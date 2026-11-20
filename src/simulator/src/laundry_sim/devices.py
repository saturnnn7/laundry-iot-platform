"""Final device list: from the config file or generated with --count."""

from __future__ import annotations

from dataclasses import dataclass

from laundry_sim.config import ConfigError, DefaultsConfig, DeviceConfig, Kind


@dataclass(frozen=True)
class Device:
    device_id: str
    location_id: str
    kind: Kind
    firmware: str


def generate_device_ids(count: int) -> list[str]:
    """sim-0001 .. sim-NNNN; four digits, wider when count > 9999."""
    if count < 1:
        raise ValueError("count must be at least 1")
    width = max(4, len(str(count)))
    return [f"sim-{n:0{width}d}" for n in range(1, count + 1)]


def generate_devices(count: int, defaults: DefaultsConfig, location_id: str | None) -> list[Device]:
    location = location_id or defaults.location_id
    if location is None:
        raise ConfigError("a location id is required (defaults.locationId or --location-id)")
    return [
        Device(device_id, location, defaults.kind, defaults.firmware)
        for device_id in generate_device_ids(count)
    ]


def resolve_devices(
    configured: list[DeviceConfig], defaults: DefaultsConfig, location_id: str | None
) -> list[Device]:
    """Fill device fields from defaults. A location id override applies to every device."""
    result: list[Device] = []
    for item in configured:
        location = location_id or item.location_id or defaults.location_id
        if location is None:
            raise ConfigError(f"device {item.device_id}: no locationId")
        result.append(
            Device(item.device_id, location, item.kind or defaults.kind, defaults.firmware)
        )
    ids = [d.device_id for d in result]
    if len(set(ids)) != len(ids):
        raise ConfigError("duplicate deviceId in config")
    return result
