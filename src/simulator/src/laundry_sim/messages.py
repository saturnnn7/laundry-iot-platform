"""MQTT v1 message builders and the inbound command model.

JSON keys match docs/api/contracts exactly. Optional fields are omitted, never null.
"""

from __future__ import annotations

import uuid
from collections.abc import Callable
from datetime import UTC, datetime
from enum import StrEnum
from typing import Annotated, Any, Literal

from pydantic import (
    AfterValidator,
    AwareDatetime,
    BaseModel,
    ConfigDict,
    Field,
    TypeAdapter,
    model_validator,
)

from laundry_sim.ids import DEVICE_ID_PATTERN, check_device_id, check_guid

SCHEMA_VERSION = 1
TS_PATTERN = r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$"

Clock = Callable[[], datetime]
IdFactory = Callable[[], str]


def system_clock() -> datetime:
    return datetime.now(UTC)


def uuid_factory() -> str:
    return str(uuid.uuid4())


def format_ts(moment: datetime) -> str:
    """UTC RFC 3339 with milliseconds and a Z suffix."""
    if moment.tzinfo is None:
        raise ValueError("timestamp must be timezone aware")
    utc = moment.astimezone(UTC)
    return f"{utc:%Y-%m-%dT%H:%M:%S}.{utc.microsecond // 1000:03d}Z"


class Channel(StrEnum):
    TELEMETRY = "telemetry"
    EVENT = "event"
    STATUS = "status"
    CMD = "cmd"
    CMD_ACK = "cmd/ack"


def topic(location_id: str, device_id: str, channel: Channel) -> str:
    return f"v1/{check_guid(location_id)}/{check_device_id(device_id)}/{channel.value}"


class Phase(StrEnum):
    IDLE = "idle"
    RUNNING = "running"
    FINISHED = "finished"
    FAULT = "fault"


class Step(StrEnum):
    FILL = "fill"
    WASH = "wash"
    RINSE = "rinse"
    SPIN = "spin"
    HEAT = "heat"
    DRY = "dry"
    COOL = "cool"


class AckStatus(StrEnum):
    OK = "ok"
    REJECTED = "rejected"
    FAILED = "failed"


class AckReason(StrEnum):
    EXPIRED = "expired"
    BUSY = "busy"
    DOOR_OPEN = "door_open"
    FAULT = "fault"
    INVALID = "invalid"


class _Out(BaseModel):
    """Outbound payload: camelCase keys, strict fields, no nulls."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True, use_enum_values=False)

    def to_dict(self) -> dict[str, Any]:
        return self.model_dump(mode="json", by_alias=True, exclude_none=True)

    def to_json(self) -> str:
        return self.model_dump_json(by_alias=True, exclude_none=True)


DeviceId = Annotated[str, Field(pattern=DEVICE_ID_PATTERN)]
Timestamp = Annotated[str, Field(pattern=TS_PATTERN)]
Uuid = Annotated[str, AfterValidator(check_guid)]


class Metrics(_Out):
    phase: Phase
    step: Step | None = None
    program_id: str | None = Field(default=None, alias="programId", max_length=64)
    remaining_sec: int | None = Field(default=None, alias="remainingSec", ge=0)
    door_closed: bool = Field(alias="doorClosed")
    locked: bool
    vibration: float | None = Field(default=None, ge=0)
    current_a: float | None = Field(default=None, alias="currentA", ge=0)
    temperature_c: float | None = Field(default=None, alias="temperatureC")
    fault_code: str | None = Field(default=None, alias="faultCode", pattern=r"^[A-Z0-9_]{2,16}$")

    @model_validator(mode="after")
    def _fault_needs_code(self) -> Metrics:
        if self.phase is Phase.FAULT and self.fault_code is None:
            raise ValueError("phase fault requires faultCode")
        return self


class Telemetry(_Out):
    schema_version: Literal[1] = Field(default=SCHEMA_VERSION, alias="schemaVersion")
    device_id: DeviceId = Field(alias="deviceId")
    boot_id: Uuid = Field(alias="bootId")
    seq: int = Field(ge=0)
    ts: Timestamp
    kind: Literal["washer", "dryer"]
    metrics: Metrics


class Event(_Out):
    schema_version: Literal[1] = Field(default=SCHEMA_VERSION, alias="schemaVersion")
    device_id: DeviceId = Field(alias="deviceId")
    boot_id: Uuid = Field(alias="bootId")
    event_id: Uuid = Field(alias="eventId")
    ts: Timestamp
    type: Literal["buttonPressed"] = "buttonPressed"


class Status(_Out):
    state: Literal["online", "offline"]
    device_id: DeviceId | None = Field(default=None, alias="deviceId")
    boot_id: Uuid | None = Field(default=None, alias="bootId")
    firmware: str | None = Field(default=None, max_length=32)
    ts: Timestamp | None = None

    @model_validator(mode="after")
    def _online_needs_identity(self) -> Status:
        if self.state == "online" and None in (self.device_id, self.boot_id, self.ts):
            raise ValueError("online status requires deviceId, bootId and ts")
        return self


class CommandAck(_Out):
    schema_version: Literal[1] = Field(default=SCHEMA_VERSION, alias="schemaVersion")
    device_id: DeviceId = Field(alias="deviceId")
    command_id: Uuid = Field(alias="commandId")
    status: AckStatus
    reason: AckReason | None = None
    ts: Timestamp


def build_telemetry(
    *,
    device_id: str,
    boot_id: str,
    seq: int,
    kind: Literal["washer", "dryer"],
    metrics: Metrics,
    clock: Clock = system_clock,
) -> Telemetry:
    return Telemetry(
        deviceId=device_id,
        bootId=boot_id,
        seq=seq,
        ts=format_ts(clock()),
        kind=kind,
        metrics=metrics,
    )


def build_button_pressed(
    *,
    device_id: str,
    boot_id: str,
    clock: Clock = system_clock,
    id_factory: IdFactory = uuid_factory,
) -> Event:
    return Event(
        deviceId=device_id,
        bootId=boot_id,
        eventId=id_factory(),
        ts=format_ts(clock()),
        type="buttonPressed",
    )


def build_status_online(
    *, device_id: str, boot_id: str, firmware: str | None, clock: Clock = system_clock
) -> Status:
    return Status(
        state="online", deviceId=device_id, bootId=boot_id, firmware=firmware, ts=format_ts(clock())
    )


def build_status_offline() -> Status:
    """Last will payload."""
    return Status(state="offline")


def build_ack(
    *,
    device_id: str,
    command_id: str,
    status: AckStatus,
    reason: AckReason | None = None,
    clock: Clock = system_clock,
) -> CommandAck:
    return CommandAck(
        deviceId=device_id,
        commandId=command_id,
        status=status,
        reason=reason,
        ts=format_ts(clock()),
    )


class _In(BaseModel):
    """Inbound payload: unknown fields are ignored (forward compatible)."""

    model_config = ConfigDict(extra="ignore", populate_by_name=True)


class ShowCodeParams(_In):
    code: str = Field(pattern=r"^[2-9A-HJ-NP-Z]{6}$")
    valid_until: AwareDatetime = Field(alias="validUntil")


class ShowMessageParams(_In):
    text: str = Field(min_length=1, max_length=40)
    seconds: int = Field(ge=1, le=60)


class UnlockParams(_In):
    display_name: str = Field(alias="displayName", min_length=1, max_length=24)


class StartParams(_In):
    program_id: str = Field(alias="programId", max_length=64)
    program_name: str | None = Field(default=None, alias="programName", max_length=40)
    minutes: int = Field(ge=5, le=240)


class SetReportIntervalParams(_In):
    idle_sec: int = Field(alias="idleSec", ge=5, le=600)
    running_sec: int = Field(alias="runningSec", ge=5, le=600)


class _CommandBase(_In):
    schema_version: Literal[1] = Field(alias="schemaVersion")
    command_id: Uuid = Field(alias="commandId")
    issued_at: AwareDatetime = Field(alias="issuedAt")
    expires_at: AwareDatetime = Field(alias="expiresAt")


class ShowCodeCommand(_CommandBase):
    command: Literal["showCode"]
    params: ShowCodeParams


class ShowMessageCommand(_CommandBase):
    command: Literal["showMessage"]
    params: ShowMessageParams


class UnlockCommand(_CommandBase):
    command: Literal["unlock"]
    params: UnlockParams


class StartCommand(_CommandBase):
    command: Literal["start"]
    params: StartParams


class StopCommand(_CommandBase):
    command: Literal["stop"]


class LockCommand(_CommandBase):
    command: Literal["lock"]


class SetReportIntervalCommand(_CommandBase):
    command: Literal["setReportInterval"]
    params: SetReportIntervalParams


Command = Annotated[
    ShowCodeCommand
    | ShowMessageCommand
    | UnlockCommand
    | StartCommand
    | StopCommand
    | LockCommand
    | SetReportIntervalCommand,
    Field(discriminator="command"),
]

_COMMAND_ADAPTER: TypeAdapter[Command] = TypeAdapter(Command)


def parse_command(payload: str | bytes) -> Command:
    """Parse an inbound cmd payload. Raises pydantic.ValidationError when it is not valid."""
    return _COMMAND_ADAPTER.validate_json(payload)
