"""Shared id patterns and small validators."""

from __future__ import annotations

import re
import uuid

DEVICE_ID_PATTERN = r"^[A-Za-z0-9_-]{3,64}$"
_DEVICE_ID_RE = re.compile(DEVICE_ID_PATTERN)


def check_device_id(value: str) -> str:
    if not _DEVICE_ID_RE.fullmatch(value):
        raise ValueError(f"deviceId must match {DEVICE_ID_PATTERN}: {value!r}")
    return value


def check_guid(value: str) -> str:
    """Return the canonical lower case form of a GUID, or raise ValueError."""
    try:
        return str(uuid.UUID(value))
    except (ValueError, AttributeError, TypeError) as exc:
        raise ValueError(f"not a valid GUID: {value!r}") from exc
