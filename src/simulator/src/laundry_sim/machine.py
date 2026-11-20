"""Pure machine model: one simulated washer or dryer.

No I/O, no sleeping, no threads and no global state. Time only moves through advance(dt_sec),
which counts device seconds. The injected clock is used for wall-clock display expiry and the
injected random.Random for metric noise. The class is not thread-safe; the caller serializes
access.

Metric ranges per step (uniform, rounded to 2 decimals):

    state            vibration   current A   temperature C
    idle, finished   0.00-0.05   0.05-0.15   20-24
    washer fill      0.10-0.40   0.50-1.50   15-25
    washer wash      0.50-1.50   2.00-4.00   30-40
    washer rinse     0.50-1.50   1.50-3.00   20-30
    washer spin      3.00-6.00   4.00-7.00   25-35
    dryer heat       0.30-0.80   6.00-9.00   60-70
    dryer dry        0.30-0.80   5.00-8.00   60-70
    dryer cool       0.30-0.80   0.50-1.50   30-50

A fault keeps the values drawn at the moment it was injected.
"""

from __future__ import annotations

import math
import random
import re
from dataclasses import dataclass
from datetime import datetime, timedelta
from enum import StrEnum
from typing import Literal

from laundry_sim.messages import (
    AckReason,
    AckStatus,
    Clock,
    Command,
    LockCommand,
    Metrics,
    Phase,
    SetReportIntervalCommand,
    ShowCodeCommand,
    ShowMessageCommand,
    StartCommand,
    Step,
    StopCommand,
    UnlockCommand,
)

__all__ = [
    "CommandResult",
    "Display",
    "DisplayKind",
    "LocalResult",
    "Machine",
    "MachineConfig",
    "Phase",
    "PhaseChange",
    "Step",
]

_EPS = 1e-9
_FAULT_CODE_RE = re.compile(r"^[A-Z0-9_]{2,16}$")

# (step, share of the cycle in percent)
_STEPS: dict[str, tuple[tuple[Step, int], ...]] = {
    "washer": ((Step.FILL, 10), (Step.WASH, 45), (Step.RINSE, 25), (Step.SPIN, 20)),
    "dryer": ((Step.HEAT, 15), (Step.DRY, 70), (Step.COOL, 15)),
}

# (vibration, current A, temperature C) as (low, high) pairs
_Ranges = tuple[tuple[float, float], tuple[float, float], tuple[float, float]]
_IDLE_RANGES: _Ranges = ((0.0, 0.05), (0.05, 0.15), (20.0, 24.0))
_STEP_RANGES: dict[Step, _Ranges] = {
    Step.FILL: ((0.1, 0.4), (0.5, 1.5), (15.0, 25.0)),
    Step.WASH: ((0.5, 1.5), (2.0, 4.0), (30.0, 40.0)),
    Step.RINSE: ((0.5, 1.5), (1.5, 3.0), (20.0, 30.0)),
    Step.SPIN: ((3.0, 6.0), (4.0, 7.0), (25.0, 35.0)),
    Step.HEAT: ((0.3, 0.8), (6.0, 9.0), (60.0, 70.0)),
    Step.DRY: ((0.3, 0.8), (5.0, 8.0), (60.0, 70.0)),
    Step.COOL: ((0.3, 0.8), (0.5, 1.5), (30.0, 50.0)),
}


class DisplayKind(StrEnum):
    CODE = "code"
    MESSAGE = "message"
    NAME = "name"


@dataclass(frozen=True)
class PhaseChange:
    old: Phase
    new: Phase


@dataclass(frozen=True)
class CommandResult:
    status: Literal[AckStatus.OK, AckStatus.REJECTED]
    reason: AckReason | None = None
    phase_change: PhaseChange | None = None


@dataclass(frozen=True)
class LocalResult:
    accepted: bool
    phase_change: PhaseChange | None = None


@dataclass(frozen=True)
class Display:
    kind: DisplayKind
    text: str
    until: datetime | None = None


@dataclass(frozen=True)
class MachineConfig:
    kind: Literal["washer", "dryer"]
    idle_interval_sec: int = 60
    running_interval_sec: int = 10
    auto_open_after_finished_sec: float | None = 30.0


def _ok(change: PhaseChange | None = None) -> CommandResult:
    return CommandResult(AckStatus.OK, None, change)


def _rejected(reason: AckReason) -> CommandResult:
    return CommandResult(AckStatus.REJECTED, reason, None)


class Machine:
    """One washer or dryer. Not thread-safe."""

    def __init__(self, config: MachineConfig, rng: random.Random, clock: Clock) -> None:
        self._config = config
        self._rng = rng
        self._clock = clock
        self._phase = Phase.IDLE
        self._step: Step | None = None
        self._program_id: str | None = None
        self._total_sec = 0.0
        self._elapsed_sec = 0.0
        self._finished_sec = 0.0
        self._door_closed = True
        self._locked = False
        self._fault_code: str | None = None
        self._frozen: tuple[float, float, float] | None = None
        self._display: Display | None = None
        self._message_left_sec = 0.0
        self._idle_interval = config.idle_interval_sec
        self._running_interval = config.running_interval_sec

    # read-only state

    @property
    def kind(self) -> Literal["washer", "dryer"]:
        return self._config.kind

    @property
    def phase(self) -> Phase:
        return self._phase

    @property
    def step(self) -> Step | None:
        return self._step

    @property
    def program_id(self) -> str | None:
        return self._program_id

    @property
    def remaining_sec(self) -> int | None:
        """Whole seconds left. None when no cycle is known (idle without a program)."""
        if self._program_id is None:
            return None
        left = round(self._total_sec - self._elapsed_sec, 6)
        return max(0, math.ceil(left))

    @property
    def door_closed(self) -> bool:
        return self._door_closed

    @property
    def locked(self) -> bool:
        return self._locked

    @property
    def fault_code(self) -> str | None:
        return self._fault_code

    # commands

    def execute(self, command: Command) -> CommandResult:
        """Run one command. Expiry, duplicates and acks are the caller's job."""
        match command:
            case ShowCodeCommand():
                self._display = Display(
                    DisplayKind.CODE, command.params.code, command.params.valid_until
                )
                return _ok()
            case ShowMessageCommand():
                seconds = command.params.seconds
                self._message_left_sec = float(seconds)
                until = self._clock() + timedelta(seconds=seconds)
                self._display = Display(DisplayKind.MESSAGE, command.params.text, until)
                return _ok()
            case UnlockCommand():
                return self._unlock(command.params.display_name)
            case StartCommand():
                return self._start(command)
            case StopCommand():
                return self._stop()
            case LockCommand():
                return self._lock()
            case SetReportIntervalCommand():
                self._idle_interval = command.params.idle_sec
                self._running_interval = command.params.running_sec
                return _ok()
        raise TypeError(f"unsupported command: {command!r}")

    def _unlock(self, name: str) -> CommandResult:
        if self._phase is Phase.RUNNING:
            return _rejected(AckReason.BUSY)
        self._locked = False
        self._display = Display(DisplayKind.NAME, name)
        return _ok()

    def _lock(self) -> CommandResult:
        if not self._door_closed:
            return _rejected(AckReason.DOOR_OPEN)
        self._locked = True
        self._clear_name()
        return _ok()

    def _start(self, command: StartCommand) -> CommandResult:
        if self._phase is Phase.FAULT:
            return _rejected(AckReason.FAULT)
        if self._phase in (Phase.RUNNING, Phase.FINISHED):
            return _rejected(AckReason.BUSY)
        if not self._door_closed:
            return _rejected(AckReason.DOOR_OPEN)
        old = self._phase
        self._phase = Phase.RUNNING
        self._locked = True
        self._program_id = command.params.program_id
        self._total_sec = float(command.params.minutes * 60)
        self._elapsed_sec = 0.0
        self._step = _STEPS[self.kind][0][0]
        self._clear_name()
        return _ok(PhaseChange(old, self._phase))

    def _stop(self) -> CommandResult:
        if self._phase is Phase.FAULT:
            return _rejected(AckReason.FAULT)
        change = None
        if self._phase is not Phase.IDLE:
            change = self._to_idle()
        self._locked = False
        return _ok(change)

    # time

    def advance(self, dt_sec: float) -> list[PhaseChange]:
        """Move device time forward. May cross several steps and phases."""
        if dt_sec < 0:
            raise ValueError("dt_sec must not be negative")
        changes: list[PhaseChange] = []
        left = dt_sec
        if self._phase is Phase.RUNNING:
            needed = self._total_sec - self._elapsed_sec
            if left + _EPS >= needed:
                left = max(0.0, left - needed)
                self._elapsed_sec = self._total_sec
                self._step = None
                self._phase = Phase.FINISHED
                self._locked = False
                self._finished_sec = 0.0
                changes.append(PhaseChange(Phase.RUNNING, Phase.FINISHED))
            else:
                self._elapsed_sec += left
                self._step = self._step_at(self._elapsed_sec)
                left = 0.0
        if self._phase is Phase.FINISHED:
            self._finished_sec += left
            limit = self._config.auto_open_after_finished_sec
            if limit is not None and self._finished_sec + _EPS >= limit:
                self._locked = False
                self._door_closed = False
                self._clear_name()
                changes.append(self._to_idle())
        self._expire_display(dt_sec)
        return changes

    def _step_at(self, elapsed: float) -> Step:
        steps = _STEPS[self.kind]
        cumulative = 0
        for step, share in steps:
            cumulative += share
            if elapsed + _EPS < self._total_sec * cumulative / 100:
                return step
        return steps[-1][0]

    # local actions

    def press_button(self) -> bool:
        """The caller publishes the event."""
        return True

    def open_door(self) -> LocalResult:
        if self._locked:
            return LocalResult(False)
        self._door_closed = False
        self._clear_name()
        change = self._to_idle() if self._phase is Phase.FINISHED else None
        return LocalResult(True, change)

    def close_door(self) -> LocalResult:
        self._door_closed = True
        return LocalResult(True)

    def inject_fault(self, code: str) -> LocalResult:
        if not _FAULT_CODE_RE.fullmatch(code):
            raise ValueError(f"fault code must match {_FAULT_CODE_RE.pattern}: {code!r}")
        if self._phase not in (Phase.IDLE, Phase.RUNNING):
            return LocalResult(False)
        old = self._phase
        self._frozen = self._draw()
        self._fault_code = code
        self._phase = Phase.FAULT
        return LocalResult(True, PhaseChange(old, Phase.FAULT))

    def clear_fault(self) -> LocalResult:
        if self._phase is not Phase.FAULT:
            return LocalResult(False)
        change = self._to_idle()
        self._locked = False
        return LocalResult(True, change)

    # reporting

    def report_interval_sec(self) -> int:
        return self._running_interval if self._phase is Phase.RUNNING else self._idle_interval

    def display(self) -> Display | None:
        shown = self._display
        if shown is None:
            return None
        if shown.kind is DisplayKind.CODE and shown.until is not None:
            if self._clock() >= shown.until:
                return None
        return shown

    def metrics(self) -> Metrics:
        if self._frozen is not None:
            vibration, current, temperature = self._frozen
        else:
            vibration, current, temperature = self._draw()
        return Metrics(
            phase=self._phase,
            step=self._step,
            programId=self._program_id,
            remainingSec=self.remaining_sec,
            doorClosed=self._door_closed,
            locked=self._locked,
            vibration=vibration,
            currentA=current,
            temperatureC=temperature,
            faultCode=self._fault_code,
        )

    # internals

    def _draw(self) -> tuple[float, float, float]:
        ranges = _STEP_RANGES[self._step] if self._step is not None else _IDLE_RANGES
        low_v, high_v = ranges[0]
        low_c, high_c = ranges[1]
        low_t, high_t = ranges[2]
        vibration = self._rng.uniform(low_v, high_v)
        current = self._rng.uniform(low_c, high_c)
        temperature = self._rng.uniform(low_t, high_t)
        return round(vibration, 2), round(current, 2), round(temperature, 2)

    def _to_idle(self) -> PhaseChange:
        old = self._phase
        self._phase = Phase.IDLE
        self._step = None
        self._program_id = None
        self._total_sec = 0.0
        self._elapsed_sec = 0.0
        self._finished_sec = 0.0
        self._fault_code = None
        self._frozen = None
        return PhaseChange(old, Phase.IDLE)

    def _clear_name(self) -> None:
        if self._display is not None and self._display.kind is DisplayKind.NAME:
            self._display = None

    def _expire_display(self, dt_sec: float) -> None:
        shown = self._display
        if shown is None:
            return
        if shown.kind is DisplayKind.MESSAGE:
            self._message_left_sec -= dt_sec
            if self._message_left_sec <= _EPS:
                self._display = None
        elif shown.kind is DisplayKind.CODE and shown.until is not None:
            if self._clock() >= shown.until:
                self._display = None
