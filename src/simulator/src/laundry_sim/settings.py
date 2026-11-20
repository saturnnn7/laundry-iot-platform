"""Final settings. Precedence: command line, environment, config file, default."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from laundry_sim.config import SimConfig

DEFAULT_HOST = "localhost"
TLS_PORT = 8883
PLAIN_PORT = 1883


@dataclass(frozen=True)
class Settings:
    host: str
    port: int
    tls: bool
    cert_dir: Path | None
    cert_dir_ignored: bool


def pick[T](cli_or_env: T | None, file_value: T | None, default: T) -> T:
    """cli_or_env is already merged by the CLI layer (command line wins over environment)."""
    if cli_or_env is not None:
        return cli_or_env
    if file_value is not None:
        return file_value
    return default


def resolve_settings(
    config: SimConfig,
    config_dir: Path,
    *,
    host: str | None,
    port: int | None,
    cert_dir: Path | None,
    insecure_local: bool,
) -> Settings:
    tls = config.broker.tls and not insecure_local
    file_cert_dir = Path(config.broker.cert_dir) if config.broker.cert_dir else None
    if file_cert_dir is not None and not file_cert_dir.is_absolute():
        file_cert_dir = config_dir / file_cert_dir
    chosen_dir = cert_dir if cert_dir is not None else file_cert_dir
    return Settings(
        host=pick(host, config.broker.host, DEFAULT_HOST),
        port=pick(port, config.broker.port, TLS_PORT if tls else PLAIN_PORT),
        tls=tls,
        cert_dir=chosen_dir if tls else None,
        cert_dir_ignored=chosen_dir is not None and not tls,
    )
