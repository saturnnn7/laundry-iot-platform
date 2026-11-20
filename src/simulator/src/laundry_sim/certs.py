"""Certificate file layout: <dir>/ca.crt, <dir>/<deviceId>.crt, <dir>/<deviceId>.key (D-20)."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from laundry_sim.ids import check_device_id


@dataclass(frozen=True)
class CertPaths:
    ca: Path
    cert: Path
    key: Path
    missing: list[Path]


def cert_paths(device_id: str, cert_dir: Path) -> CertPaths:
    check_device_id(device_id)
    ca = cert_dir / "ca.crt"
    cert = cert_dir / f"{device_id}.crt"
    key = cert_dir / f"{device_id}.key"
    missing = [p for p in (ca, cert, key) if not p.is_file()]
    return CertPaths(ca=ca, cert=cert, key=key, missing=missing)
