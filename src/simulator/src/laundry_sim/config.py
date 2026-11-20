"""YAML config models and loader. Unknown keys are rejected on every model."""

from __future__ import annotations

from pathlib import Path
from typing import Annotated, Literal

import yaml
from pydantic import AfterValidator, BaseModel, ConfigDict, Field, ValidationError

from laundry_sim.ids import check_device_id, check_guid

DeviceId = Annotated[str, AfterValidator(check_device_id)]
Guid = Annotated[str, AfterValidator(check_guid)]
Kind = Literal["washer", "dryer"]

DEFAULT_FIRMWARE = "sim-0.1.0"
DEFAULT_KIND: Kind = "washer"


class ConfigError(Exception):
    """The config file cannot be read or is invalid."""


class _Model(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)


class BrokerConfig(_Model):
    host: str | None = None
    port: int | None = Field(default=None, ge=1, le=65535)
    tls: bool = True
    cert_dir: str | None = Field(default=None, alias="certDir")


class DefaultsConfig(_Model):
    location_id: Guid | None = Field(default=None, alias="locationId")
    kind: Kind = DEFAULT_KIND
    firmware: str = Field(default=DEFAULT_FIRMWARE, max_length=32)


class DeviceConfig(_Model):
    device_id: DeviceId = Field(alias="deviceId")
    location_id: Guid | None = Field(default=None, alias="locationId")
    kind: Kind | None = None


class SimConfig(_Model):
    broker: BrokerConfig = Field(default_factory=BrokerConfig)
    defaults: DefaultsConfig = Field(default_factory=DefaultsConfig)
    devices: list[DeviceConfig] = Field(default_factory=list)


def parse_config(text: str) -> SimConfig:
    try:
        data = yaml.safe_load(text)
    except yaml.YAMLError as exc:
        raise ConfigError(f"invalid YAML: {exc}") from exc
    if data is None:
        data = {}
    if not isinstance(data, dict):
        raise ConfigError("config root must be a mapping")
    try:
        return SimConfig.model_validate(data)
    except ValidationError as exc:
        raise ConfigError(str(exc)) from exc


def load_config(path: Path) -> SimConfig:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as exc:
        raise ConfigError(f"cannot read config {path}: {exc}") from exc
    return parse_config(text)
