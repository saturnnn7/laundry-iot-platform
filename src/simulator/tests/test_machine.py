from __future__ import annotations

import json
import random
from collections.abc import Callable
from datetime import UTC, datetime, timedelta
from typing import Any

import pytest
from test_contract_schemas import assert_valid

from laundry_sim.machine import (
    DisplayKind,
    LocalResult,
    Machine,
    MachineConfig,
    PhaseChange,
)
from laundry_sim.messages import (
    AckReason,
    AckStatus,
    Command,
    Phase,
    Step,
    build_telemetry,
    parse_command,
)

T0 = datetime(2026, 1, 2, 3, 4, 5, tzinfo=UTC)
BOOT = "11111111-1111-4111-8111-111111111111"
CMD_ID = "22222222-2222-4222-8222-222222222222"


class FakeClock:
    def __init__(self) -> None:
        self.now = T0

    def __call__(self) -> datetime:
        return self.now


def cmd(name: str, params: dict[str, Any] | None = None) -> Command:
    payload: dict[str, Any] = {
        "schemaVersion": 1,
        "commandId": CMD_ID,
        "command": name,
        "issuedAt": "2026-01-02T03:04:05.000Z",
        "expiresAt": "2026-01-02T03:04:15.000Z",
    }
    if params is not None:
        payload["params"] = params
    return parse_command(json.dumps(payload))


def start_cmd(minutes: int = 40, program: str = "cotton") -> Command:
    return cmd("start", {"programId": program, "minutes": minutes})


def make(
    kind: str = "washer", *, seed: int = 1, auto_open: float | None = 30.0
) -> tuple[Machine, FakeClock]:
    clock = FakeClock()
    config = MachineConfig(kind=kind, auto_open_after_finished_sec=auto_open)  # type: ignore[arg-type]
    return Machine(config, random.Random(seed), clock), clock


def to_running(m: Machine, minutes: int = 40) -> None:
    assert m.execute(start_cmd(minutes)).status is AckStatus.OK


def to_finished(m: Machine, minutes: int = 40) -> None:
    to_running(m, minutes)
    m.advance(minutes * 60)
    assert m.phase is Phase.FINISHED


def to_fault(m: Machine) -> None:
    assert m.inject_fault("E_TEST").accepted
    assert m.phase is Phase.FAULT


def to_door_open(m: Machine) -> None:
    assert m.open_door().accepted


SETUPS: dict[str, Callable[[Machine], None]] = {
    "idle": lambda m: None,
    "running": to_running,
    "finished": to_finished,
    "fault": to_fault,
    "door_open": to_door_open,
}

# (setup, command, status, reason)
RULE_CASES = [
    ("running", start_cmd(), AckStatus.REJECTED, AckReason.BUSY),
    ("finished", start_cmd(), AckStatus.REJECTED, AckReason.BUSY),
    ("door_open", start_cmd(), AckStatus.REJECTED, AckReason.DOOR_OPEN),
    ("fault", start_cmd(), AckStatus.REJECTED, AckReason.FAULT),
    ("idle", start_cmd(), AckStatus.OK, None),
    ("door_open", cmd("lock"), AckStatus.REJECTED, AckReason.DOOR_OPEN),
    ("running", cmd("unlock", {"displayName": "Ann"}), AckStatus.REJECTED, AckReason.BUSY),
    ("idle", cmd("unlock", {"displayName": "Ann"}), AckStatus.OK, None),
    ("finished", cmd("unlock", {"displayName": "Ann"}), AckStatus.OK, None),
    ("idle", cmd("stop"), AckStatus.OK, None),
    ("running", cmd("stop"), AckStatus.OK, None),
    ("finished", cmd("stop"), AckStatus.OK, None),
    ("fault", cmd("stop"), AckStatus.REJECTED, AckReason.FAULT),
]


@pytest.mark.parametrize(("setup", "command", "status", "reason"), RULE_CASES)
def test_rule_cases(
    setup: str, command: Command, status: AckStatus, reason: AckReason | None
) -> None:
    m, _ = make()
    SETUPS[setup](m)
    result = m.execute(command)
    assert (result.status, result.reason) == (status, reason)


def test_rejected_commands_change_nothing() -> None:
    m, _ = make()
    to_running(m)
    before = (m.phase, m.locked, m.remaining_sec, m.door_closed)
    result = m.execute(start_cmd())
    assert result.phase_change is None
    assert (m.phase, m.locked, m.remaining_sec, m.door_closed) == before


def test_start_reports_phase_change_and_locks() -> None:
    m, _ = make()
    result = m.execute(start_cmd(40, "cotton-40"))
    assert result.phase_change == PhaseChange(Phase.IDLE, Phase.RUNNING)
    assert m.locked and m.program_id == "cotton-40"
    assert m.remaining_sec == 2400 and m.step is Step.FILL


@pytest.mark.parametrize("setup", ["running", "finished"])
def test_stop_goes_idle_and_unlocks(setup: str) -> None:
    m, _ = make()
    SETUPS[setup](m)
    result = m.execute(cmd("stop"))
    assert result.status is AckStatus.OK
    assert result.phase_change == PhaseChange(Phase(setup), Phase.IDLE)
    assert m.phase is Phase.IDLE and not m.locked and m.program_id is None


def test_stop_idle_has_no_phase_change() -> None:
    m, _ = make()
    assert m.execute(cmd("stop")).phase_change is None


@pytest.mark.parametrize("setup", ["idle", "running", "finished", "fault"])
def test_lock_with_door_closed(setup: str) -> None:
    m, _ = make()
    SETUPS[setup](m)
    result = m.execute(cmd("lock"))
    assert result.status is AckStatus.OK and result.phase_change is None
    assert m.locked


def test_unlock_sets_unlocked_and_stays_until_lock_or_start() -> None:
    m, _ = make()
    m.execute(cmd("lock"))
    m.execute(cmd("unlock", {"displayName": "Ann"}))
    assert not m.locked
    m.advance(100)
    assert not m.locked
    m.execute(start_cmd())
    assert m.locked


def test_door_cannot_open_while_locked() -> None:
    m, _ = make()
    m.execute(cmd("lock"))
    assert not m.open_door().accepted and m.door_closed
    m.execute(cmd("unlock", {"displayName": "Ann"}))
    assert m.open_door().accepted and not m.door_closed
    assert m.close_door().accepted and m.door_closed


def test_start_works_after_door_closed_again() -> None:
    m, _ = make()
    m.open_door()
    m.close_door()
    assert m.execute(start_cmd()).status is AckStatus.OK


# cycles


def final_state(m: Machine) -> tuple[Any, ...]:
    return (m.phase, m.step, m.program_id, m.remaining_sec, m.door_closed, m.locked)


@pytest.mark.parametrize(
    ("kind", "order"),
    [
        ("washer", [Step.FILL, Step.WASH, Step.RINSE, Step.SPIN]),
        ("dryer", [Step.HEAT, Step.DRY, Step.COOL]),
    ],
)
def test_full_cycle_small_and_big_steps(kind: str, order: list[Step]) -> None:
    small, _ = make(kind, auto_open=None)
    big, _ = make(kind, auto_open=None)
    to_running(small, 30)
    to_running(big, 30)
    seen: list[Step] = []
    remaining: list[int] = []
    changes: list[PhaseChange] = []
    for _ in range(1800):
        changes += small.advance(1.0)
        remaining.append(small.remaining_sec or 0)
        if small.step is not None and (not seen or seen[-1] is not small.step):
            seen.append(small.step)
    big_changes = big.advance(1800)
    assert seen == order
    assert remaining == sorted(remaining, reverse=True) and remaining[-1] == 0
    assert all(r >= 0 for r in remaining)
    assert changes == big_changes == [PhaseChange(Phase.RUNNING, Phase.FINISHED)]
    assert final_state(small) == final_state(big)
    assert small.phase is Phase.FINISHED and small.step is None and small.program_id == "cotton"
    assert not small.locked and small.remaining_sec == 0


def test_fractional_steps_match_one_big_step() -> None:
    a, _ = make(auto_open=None)
    b, _ = make(auto_open=None)
    to_running(a, 5)
    to_running(b, 5)
    for _ in range(3000):
        a.advance(0.1)
    b.advance(300)
    assert final_state(a) == final_state(b)
    assert a.phase is Phase.FINISHED


def test_share_boundaries_washer() -> None:
    m, _ = make()
    to_running(m, 10)  # 600 s: fill 60, wash 270, rinse 150, spin 120
    m.advance(59)
    assert m.step is Step.FILL
    m.advance(1)
    assert m.step is Step.WASH
    m.advance(270)
    assert m.step is Step.RINSE
    m.advance(150)
    assert m.step is Step.SPIN
    assert m.remaining_sec == 120


def test_one_step_spans_cycle_and_auto_open() -> None:
    m, _ = make()
    to_running(m, 10)
    changes = m.advance(600 + 30)
    assert changes == [
        PhaseChange(Phase.RUNNING, Phase.FINISHED),
        PhaseChange(Phase.FINISHED, Phase.IDLE),
    ]
    assert m.phase is Phase.IDLE and not m.door_closed and m.program_id is None


def test_finished_unlocked_then_auto_open() -> None:
    m, _ = make()
    to_finished(m, 10)
    assert not m.locked and m.program_id == "cotton"
    assert m.advance(29) == []
    assert m.phase is Phase.FINISHED
    assert m.advance(1) == [PhaseChange(Phase.FINISHED, Phase.IDLE)]
    assert not m.door_closed and m.program_id is None and m.remaining_sec is None


def test_auto_open_disabled() -> None:
    m, _ = make(auto_open=None)
    to_finished(m, 10)
    assert m.advance(100000) == []
    assert m.phase is Phase.FINISHED


def test_manual_open_in_finished_goes_idle() -> None:
    m, _ = make()
    to_finished(m, 10)
    result = m.open_door()
    assert result.accepted and result.phase_change == PhaseChange(Phase.FINISHED, Phase.IDLE)
    assert m.program_id is None and m.phase is Phase.IDLE


def test_advance_rejects_negative() -> None:
    m, _ = make()
    with pytest.raises(ValueError):
        m.advance(-1)


# faults


@pytest.mark.parametrize("setup", ["idle", "running"])
def test_fault_inject_and_clear(setup: str) -> None:
    m, _ = make()
    SETUPS[setup](m)
    old = m.phase
    result = m.inject_fault("E_UNBALANCE")
    assert result.accepted and result.phase_change == PhaseChange(old, Phase.FAULT)
    assert m.fault_code == "E_UNBALANCE"
    m.advance(1000)
    assert m.phase is Phase.FAULT
    cleared = m.clear_fault()
    assert cleared.accepted and cleared.phase_change == PhaseChange(Phase.FAULT, Phase.IDLE)
    assert m.fault_code is None and m.program_id is None and m.step is None and not m.locked


def test_fault_refused_in_finished_and_fault() -> None:
    m, _ = make()
    to_finished(m)
    assert m.inject_fault("E_TEST") == LocalResult(False)
    assert m.phase is Phase.FINISHED
    m2, _ = make()
    to_fault(m2)
    assert not m2.inject_fault("E_OTHER").accepted and m2.fault_code == "E_TEST"


def test_clear_fault_without_fault() -> None:
    m, _ = make()
    assert not m.clear_fault().accepted


def test_bad_fault_code() -> None:
    m, _ = make()
    with pytest.raises(ValueError):
        m.inject_fault("bad code")


def test_fault_keeps_last_values() -> None:
    m, _ = make()
    to_running(m)
    m.advance(300)
    step = m.step
    remaining = m.remaining_sec
    m.inject_fault("E_TEST")
    first = m.metrics()
    second = m.metrics()
    assert first == second
    assert first.step is step and first.remaining_sec == remaining
    assert first.fault_code == "E_TEST"


# reporting


def test_report_interval_per_phase() -> None:
    m, _ = make()
    assert m.report_interval_sec() == 60
    to_running(m)
    assert m.report_interval_sec() == 10
    m.inject_fault("E_TEST")
    assert m.report_interval_sec() == 60


def test_set_report_interval_changes_both() -> None:
    m, _ = make()
    result = m.execute(cmd("setReportInterval", {"idleSec": 120, "runningSec": 5}))
    assert result.status is AckStatus.OK and result.phase_change is None
    assert m.report_interval_sec() == 120
    to_running(m)
    assert m.report_interval_sec() == 5


# display


def test_display_code_expires_by_clock() -> None:
    m, clock = make()
    until = (T0 + timedelta(seconds=60)).isoformat().replace("+00:00", "Z")
    m.execute(cmd("showCode", {"code": "ABC234", "validUntil": until}))
    shown = m.display()
    assert shown is not None and (shown.kind, shown.text) == (DisplayKind.CODE, "ABC234")
    clock.now = T0 + timedelta(seconds=59)
    assert m.display() is not None
    clock.now = T0 + timedelta(seconds=60)
    assert m.display() is None
    m.advance(0)
    assert m.display() is None


def test_display_message_expires_in_device_time() -> None:
    m, _ = make()
    m.execute(cmd("showMessage", {"text": "Hello", "seconds": 5}))
    shown = m.display()
    assert shown is not None and (shown.kind, shown.text) == (DisplayKind.MESSAGE, "Hello")
    m.advance(4)
    assert m.display() is not None
    m.advance(1)
    assert m.display() is None


def test_display_latest_wins() -> None:
    m, _ = make()
    m.execute(cmd("showMessage", {"text": "One", "seconds": 30}))
    m.execute(cmd("unlock", {"displayName": "Ann"}))
    shown = m.display()
    assert shown is not None and (shown.kind, shown.text) == (DisplayKind.NAME, "Ann")
    m.execute(cmd("showMessage", {"text": "Two", "seconds": 30}))
    shown = m.display()
    assert shown is not None and shown.text == "Two"


@pytest.mark.parametrize("action", ["open", "lock", "start"])
def test_name_cleared_by_door_lock_or_start(action: str) -> None:
    m, _ = make()
    m.execute(cmd("unlock", {"displayName": "Ann"}))
    assert m.display() is not None
    if action == "open":
        m.open_door()
    elif action == "lock":
        m.execute(cmd("lock"))
    else:
        m.execute(start_cmd())
    assert m.display() is None


def test_name_stays_without_those_events() -> None:
    m, _ = make()
    m.execute(cmd("unlock", {"displayName": "Ann"}))
    m.advance(10000)
    assert m.display() is not None


def test_message_survives_door_and_start() -> None:
    m, _ = make()
    m.execute(cmd("showMessage", {"text": "Hi", "seconds": 60}))
    m.execute(start_cmd())
    assert m.display() is not None


def test_press_button() -> None:
    m, _ = make()
    assert m.press_button() is True


# metrics


def sample(seed: int) -> list[Any]:
    m, _ = make(seed=seed)
    out = [m.metrics()]
    to_running(m, 10)
    for _ in range(20):
        m.advance(30)
        out.append(m.metrics())
    return out


def test_same_seed_same_metrics_different_seed_differs() -> None:
    assert sample(7) == sample(7)
    assert sample(7) != sample(8)


def test_idle_metric_ranges() -> None:
    m, _ = make()
    for _ in range(50):
        metrics = m.metrics()
        assert 0 <= (metrics.vibration or 0) <= 0.05
        assert 0.05 <= (metrics.current_a or 0) <= 0.15
        assert 20 <= (metrics.temperature_c or 0) <= 24


def test_spin_is_strongest_and_dryer_is_hot() -> None:
    m, _ = make("washer")
    to_running(m, 10)
    m.advance(300)  # wash
    wash = m.metrics()
    m.advance(200)  # spin
    assert m.step is Step.SPIN
    spin = m.metrics()
    assert (spin.vibration or 0) > (wash.vibration or 0)
    assert (spin.current_a or 0) > (wash.current_a or 0)
    assert (wash.temperature_c or 0) <= 40
    d, _ = make("dryer")
    to_running(d, 10)
    d.advance(200)
    assert d.step is Step.DRY
    assert 60 <= (d.metrics().temperature_c or 0) <= 70


def test_metrics_omit_cycle_fields_when_idle() -> None:
    m, _ = make()
    data = m.metrics().to_dict()
    assert {"step", "programId", "remainingSec", "faultCode"}.isdisjoint(data)


# schema validity for every phase and step


def check_schema(m: Machine, seq: int = 0) -> None:
    msg = build_telemetry(
        device_id="sim-0001",
        boot_id=BOOT,
        seq=seq,
        kind=m.kind,
        metrics=m.metrics(),
        clock=lambda: T0,
    )
    assert_valid("telemetry", msg.to_dict())


@pytest.mark.parametrize("kind", ["washer", "dryer"])
def test_telemetry_valid_for_every_phase_and_step(kind: str) -> None:
    m, _ = make(kind, auto_open=None)
    seen_phases: set[Phase] = set()
    seen_steps: set[Step] = set()

    def visit() -> None:
        check_schema(m)
        seen_phases.add(m.phase)
        if m.step is not None:
            seen_steps.add(m.step)

    visit()  # idle
    to_running(m, 20)
    for _ in range(40):
        visit()
        m.advance(30)
    visit()  # finished
    m.open_door()
    visit()  # idle again, door open
    m.close_door()
    to_running(m, 20)
    m.advance(100)
    m.inject_fault("E_FAULT_42")
    visit()  # fault in running
    m.clear_fault()
    m.inject_fault("ZZ")
    visit()  # fault in idle
    assert seen_phases == set(Phase)
    expected = {Step.FILL, Step.WASH, Step.RINSE, Step.SPIN}
    if kind == "dryer":
        expected = {Step.HEAT, Step.DRY, Step.COOL}
    assert seen_steps == expected
