"""Builders and the command model checked against the v1 JSON Schemas."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from functools import cache
from pathlib import Path
from typing import Any

import pytest
from jsonschema import Draft202012Validator, FormatChecker
from pydantic import ValidationError

from laundry_sim.messages import (
    AckReason,
    AckStatus,
    Channel,
    Metrics,
    Phase,
    StartCommand,
    Step,
    build_ack,
    build_button_pressed,
    build_status_offline,
    build_status_online,
    build_telemetry,
    format_ts,
    parse_command,
    topic,
)

DEVICE = "sim-0001"
BOOT = "11111111-1111-4111-8111-111111111111"
CMD_ID = "22222222-2222-4222-8222-222222222222"
LOCATION = "3f2b8c1e-5a47-4d1e-9c0b-7a6d2e4f8b10"
NOW = datetime(2026, 1, 2, 3, 4, 5, 678901, tzinfo=UTC)

SCHEMA_FILES = {
    "telemetry": "telemetry.schema.json",
    "event": "event.schema.json",
    "status": "status.schema.json",
    "command": "command.schema.json",
    "command-ack": "command-ack.schema.json",
}


def _contracts_dir() -> Path:
    for parent in Path(__file__).resolve().parents:
        candidate = parent / "docs" / "api" / "contracts"
        if candidate.is_dir():
            return candidate
    pytest.fail("docs/api/contracts not found above tests/; run the tests inside the repository")


@cache
def _validator(channel: str) -> Draft202012Validator:
    path = _contracts_dir() / SCHEMA_FILES[channel]
    if not path.is_file():
        pytest.fail(f"schema file missing: {path}")
    schema = json.loads(path.read_text(encoding="utf-8"))
    return Draft202012Validator(schema, format_checker=FormatChecker())


def assert_valid(channel: str, payload: dict[str, Any]) -> None:
    errors = sorted(_validator(channel).iter_errors(payload), key=lambda e: list(e.path))
    assert not errors, [e.message for e in errors]


def assert_invalid(channel: str, payload: dict[str, Any]) -> None:
    assert not _validator(channel).is_valid(payload)


def clock() -> datetime:
    return NOW


def metrics(**kw: Any) -> Metrics:
    base: dict[str, Any] = {"phase": Phase.IDLE, "doorClosed": True, "locked": False}
    base.update(kw)
    return Metrics(**base)


def test_format_ts_utc_millis_z() -> None:
    assert format_ts(NOW) == "2026-01-02T03:04:05.678Z"
    assert format_ts(datetime.fromisoformat("2026-01-02T05:04:05+02:00")) == (
        "2026-01-02T03:04:05.000Z"
    )
    with pytest.raises(ValueError):
        format_ts(datetime(2026, 1, 1))


def test_topics() -> None:
    assert topic(LOCATION, DEVICE, Channel.TELEMETRY) == f"v1/{LOCATION}/{DEVICE}/telemetry"
    assert topic(LOCATION, DEVICE, Channel.CMD_ACK).endswith("/cmd/ack")
    with pytest.raises(ValueError):
        topic("not-a-guid", DEVICE, Channel.EVENT)
    with pytest.raises(ValueError):
        topic(LOCATION, "a/b", Channel.EVENT)


def test_telemetry_valid_and_compact() -> None:
    full = metrics(
        phase=Phase.RUNNING,
        step=Step.WASH,
        programId="cotton-40",
        remainingSec=900,
        vibration=1.5,
        currentA=2.0,
        temperatureC=40.0,
        locked=True,
    )
    msg = build_telemetry(
        device_id=DEVICE, boot_id=BOOT, seq=0, kind="washer", metrics=full, clock=clock
    )
    assert_valid("telemetry", msg.to_dict())
    raw = msg.to_json()
    assert ": " not in raw and ", " not in raw
    assert json.loads(raw)["ts"] == "2026-01-02T03:04:05.678Z"


def test_telemetry_omits_optional_fields() -> None:
    msg = build_telemetry(
        device_id=DEVICE, boot_id=BOOT, seq=3, kind="dryer", metrics=metrics(), clock=clock
    )
    data = msg.to_dict()
    assert set(data["metrics"]) == {"phase", "doorClosed", "locked"}
    assert "null" not in msg.to_json()
    assert_valid("telemetry", data)


def test_fault_without_code_rejected_by_schema_and_builder() -> None:
    payload = {
        "schemaVersion": 1,
        "deviceId": DEVICE,
        "bootId": BOOT,
        "seq": 1,
        "ts": "2026-01-02T03:04:05.678Z",
        "kind": "washer",
        "metrics": {"phase": "fault", "doorClosed": True, "locked": True},
    }
    assert_invalid("telemetry", payload)
    with pytest.raises(ValidationError):
        metrics(phase=Phase.FAULT)


def test_fault_with_code_valid() -> None:
    msg = build_telemetry(
        device_id=DEVICE,
        boot_id=BOOT,
        seq=1,
        kind="washer",
        metrics=metrics(phase=Phase.FAULT, faultCode="E_UNBALANCE"),
        clock=clock,
    )
    assert_valid("telemetry", msg.to_dict())


@pytest.mark.parametrize(
    "kw",
    [
        {"seq": -1},
        {"device_id": "ab"},
        {"boot_id": "nope"},
        {"kind": "oven"},
    ],
)
def test_telemetry_builder_rejects_bad_values(kw: dict[str, Any]) -> None:
    args: dict[str, Any] = {
        "device_id": DEVICE,
        "boot_id": BOOT,
        "seq": 0,
        "kind": "washer",
        "metrics": metrics(),
        "clock": clock,
    }
    args.update(kw)
    with pytest.raises(ValidationError):
        build_telemetry(**args)


@pytest.mark.parametrize("kw", [{"remainingSec": -1}, {"vibration": -0.1}, {"faultCode": "bad"}])
def test_metrics_reject_out_of_range(kw: dict[str, Any]) -> None:
    with pytest.raises(ValidationError):
        metrics(**kw)


def test_telemetry_schema_rejects_bad_payloads() -> None:
    good = build_telemetry(
        device_id=DEVICE, boot_id=BOOT, seq=0, kind="washer", metrics=metrics(), clock=clock
    ).to_dict()
    for key in ("schemaVersion", "deviceId", "bootId", "seq", "ts", "kind", "metrics"):
        broken = {k: v for k, v in good.items() if k != key}
        assert_invalid("telemetry", broken)
    assert_invalid("telemetry", {**good, "bootId": "not-a-uuid"})
    assert_invalid("telemetry", {**good, "ts": "yesterday"})


def test_event_valid_and_deterministic() -> None:
    msg = build_button_pressed(
        device_id=DEVICE, boot_id=BOOT, clock=clock, id_factory=lambda: CMD_ID
    )
    data = msg.to_dict()
    assert data["eventId"] == CMD_ID and data["type"] == "buttonPressed"
    assert_valid("event", data)
    assert_invalid("event", {**data, "type": "doorKicked"})
    assert_invalid("event", {k: v for k, v in data.items() if k != "eventId"})


def test_status_online_offline() -> None:
    online = build_status_online(device_id=DEVICE, boot_id=BOOT, firmware="1.0.0", clock=clock)
    assert_valid("status", online.to_dict())
    assert online.to_dict()["state"] == "online"
    offline = build_status_offline()
    assert offline.to_json() == '{"state":"offline"}'
    assert_valid("status", offline.to_dict())
    assert_invalid("status", {"state": "online"})
    assert_invalid("status", {"state": "sleeping"})


def test_status_online_without_firmware_omits_it() -> None:
    online = build_status_online(device_id=DEVICE, boot_id=BOOT, firmware=None, clock=clock)
    assert "firmware" not in online.to_dict()
    assert_valid("status", online.to_dict())


@pytest.mark.parametrize("reason", list(AckReason))
def test_ack_reasons_valid(reason: AckReason) -> None:
    msg = build_ack(
        device_id=DEVICE,
        command_id=CMD_ID,
        status=AckStatus.REJECTED,
        reason=reason,
        clock=clock,
    )
    assert_valid("command-ack", msg.to_dict())


def test_ack_reason_enum_is_exact() -> None:
    assert {r.value for r in AckReason} == {"expired", "busy", "door_open", "fault", "invalid"}
    with pytest.raises(ValidationError):
        build_ack(
            device_id=DEVICE,
            command_id=CMD_ID,
            status=AckStatus.REJECTED,
            reason="whatever",  # type: ignore[arg-type]
            clock=clock,
        )


def test_ack_ok_omits_reason() -> None:
    msg = build_ack(device_id=DEVICE, command_id=CMD_ID, status=AckStatus.OK, clock=clock)
    assert "reason" not in msg.to_dict()
    assert_valid("command-ack", msg.to_dict())
    assert_invalid("command-ack", {**msg.to_dict(), "status": "maybe"})


# every command in command.schema.json: (name, valid params, [bad params])
COMMANDS: list[tuple[str, dict[str, Any] | None, list[dict[str, Any]]]] = [
    (
        "showCode",
        {"code": "ABC234", "validUntil": "2026-01-02T03:05:05.000Z"},
        [{"code": "ABC234"}, {"code": "ABC0O1", "validUntil": "2026-01-02T03:05:05.000Z"}],
    ),
    (
        "showMessage",
        {"text": "Hello", "seconds": 10},
        [{"text": "Hello"}, {"text": "x" * 41, "seconds": 10}, {"text": "Hi", "seconds": 61}],
    ),
    ("unlock", {"displayName": "Ann"}, [{}, {"displayName": "x" * 25}]),
    (
        "start",
        {"programId": "cotton-40", "minutes": 45, "programName": "Cotton"},
        [{"programId": "p"}, {"programId": "p", "minutes": 4}, {"programId": "p", "minutes": 241}],
    ),
    ("stop", None, []),
    ("lock", None, []),
    (
        "setReportInterval",
        {"idleSec": 60, "runningSec": 10},
        [{"idleSec": 60}, {"idleSec": 4, "runningSec": 10}, {"idleSec": 60, "runningSec": 601}],
    ),
]


def command_payload(name: str, params: dict[str, Any] | None) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "schemaVersion": 1,
        "commandId": CMD_ID,
        "command": name,
        "issuedAt": "2026-01-02T03:04:05.000Z",
        "expiresAt": "2026-01-02T03:04:15.000Z",
    }
    if params is not None:
        payload["params"] = params
    return payload


@pytest.mark.parametrize(("name", "params", "bad"), COMMANDS)
def test_every_command(name: str, params: dict[str, Any] | None, bad: list[dict[str, Any]]) -> None:
    good = command_payload(name, params)
    assert_valid("command", good)
    parsed = parse_command(json.dumps(good))
    assert parsed.command == name
    assert str(parsed.command_id) == CMD_ID
    for bad_params in bad:
        payload = command_payload(name, bad_params)
        assert_invalid("command", payload)
        with pytest.raises(ValidationError):
            parse_command(json.dumps(payload))
    if params is not None:
        no_params = command_payload(name, None)
        assert_invalid("command", no_params)
        with pytest.raises(ValidationError):
            parse_command(json.dumps(no_params))


def test_command_params_are_typed() -> None:
    parsed = parse_command(json.dumps(command_payload("start", {"programId": "p", "minutes": 30})))
    assert isinstance(parsed, StartCommand)
    assert parsed.params.minutes == 30 and parsed.params.program_name is None


def test_command_unknown_name_and_fields() -> None:
    with pytest.raises(ValidationError):
        parse_command(json.dumps(command_payload("selfDestruct", None)))
    assert_invalid("command", command_payload("selfDestruct", None))
    extra = {**command_payload("stop", None), "futureField": 1}
    assert parse_command(json.dumps(extra)).command == "stop"
    with pytest.raises(ValidationError):
        parse_command(b"not json")
