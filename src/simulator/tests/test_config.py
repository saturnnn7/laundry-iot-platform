from __future__ import annotations

from pathlib import Path

import pytest
from typer.testing import CliRunner

from laundry_sim.cli import app
from laundry_sim.config import ConfigError, parse_config
from laundry_sim.devices import generate_device_ids, generate_devices, resolve_devices
from laundry_sim.settings import pick, resolve_settings

LOC = "3f2b8c1e-5a47-4d1e-9c0b-7a6d2e4f8b10"
LOC2 = "aaaaaaaa-5a47-4d1e-9c0b-7a6d2e4f8b10"

BASE = f"""
defaults:
  locationId: {LOC}
devices:
  - deviceId: sim-0001
"""


def write(tmp_path: Path, text: str) -> Path:
    path = tmp_path / "devices.yaml"
    path.write_text(text, encoding="utf-8")
    return path


def test_valid_config_fills_defaults() -> None:
    cfg = parse_config(BASE)
    devices = resolve_devices(cfg.devices, cfg.defaults, None)
    assert [d.device_id for d in devices] == ["sim-0001"]
    assert devices[0].location_id == LOC and devices[0].kind == "washer"


@pytest.mark.parametrize("bad", ["ab", "x" * 65, "has space", "dot.id", "sim/1"])
def test_bad_device_id(bad: str) -> None:
    with pytest.raises(ConfigError):
        parse_config(f"devices:\n  - deviceId: '{bad}'\n    locationId: {LOC}\n")


def test_bad_guid() -> None:
    with pytest.raises(ConfigError):
        parse_config("devices:\n  - deviceId: sim-0001\n    locationId: not-a-guid\n")
    with pytest.raises(ConfigError):
        parse_config("defaults:\n  locationId: 123\n")


@pytest.mark.parametrize(
    "text",
    [
        "surprise: 1\n",
        "broker:\n  extra: 1\n",
        "defaults:\n  colour: red\n",
        f"devices:\n  - deviceId: sim-0001\n    locationId: {LOC}\n    nope: 1\n",
    ],
)
def test_unknown_key_rejected(text: str) -> None:
    with pytest.raises(ConfigError):
        parse_config(text)


@pytest.mark.parametrize(
    "text",
    [
        "broker:\n  tls_verify: false\n",
        "broker:\n  verify: false\n",
        "broker:\n  verifyHostname: false\n",
        "broker:\n  insecure: true\n",
        "verify: false\n",
    ],
)
def test_verification_off_key_rejected(text: str) -> None:
    with pytest.raises(ConfigError):
        parse_config(text)


def test_bad_kind_and_duplicates() -> None:
    with pytest.raises(ConfigError):
        parse_config(f"devices:\n  - deviceId: sim-0001\n    locationId: {LOC}\n    kind: oven\n")
    cfg = parse_config(BASE + "  - deviceId: sim-0001\n")
    with pytest.raises(ConfigError, match="duplicate"):
        resolve_devices(cfg.devices, cfg.defaults, None)


def test_count_generation() -> None:
    assert generate_device_ids(3) == ["sim-0001", "sim-0002", "sim-0003"]
    ids = generate_device_ids(10000)
    assert ids[0] == "sim-00001" and ids[-1] == "sim-10000"
    assert generate_device_ids(9999)[-1] == "sim-9999"
    with pytest.raises(ValueError):
        generate_device_ids(0)
    cfg = parse_config(BASE)
    devices = generate_devices(2, cfg.defaults, None)
    assert [d.device_id for d in devices] == ["sim-0001", "sim-0002"]
    assert generate_devices(1, cfg.defaults, LOC2)[0].location_id == LOC2
    with pytest.raises(ConfigError):
        generate_devices(1, parse_config("").defaults, None)


def test_pick_precedence() -> None:
    assert pick("cli", "file", "default") == "cli"
    assert pick(None, "file", "default") == "file"
    assert pick(None, None, "default") == "default"


def test_insecure_local_defaults(tmp_path: Path) -> None:
    cfg = parse_config("")
    tls = resolve_settings(cfg, tmp_path, host=None, port=None, cert_dir=None, insecure_local=False)
    assert (tls.host, tls.port, tls.tls) == ("localhost", 8883, True)
    local = resolve_settings(
        cfg, tmp_path, host=None, port=None, cert_dir=None, insecure_local=True
    )
    assert (local.host, local.port, local.tls) == ("localhost", 1883, False)
    ignored = resolve_settings(
        cfg, tmp_path, host=None, port=None, cert_dir=Path("x"), insecure_local=True
    )
    assert ignored.cert_dir is None and ignored.cert_dir_ignored


def test_config_cert_dir_is_relative_to_config_dir(tmp_path: Path) -> None:
    cfg = parse_config("broker:\n  certDir: pki\n")
    settings = resolve_settings(
        cfg, tmp_path, host=None, port=None, cert_dir=None, insecure_local=False
    )
    assert settings.cert_dir == tmp_path / "pki"


FILE = f"""
broker:
  host: file-host
  port: 9001
defaults:
  locationId: {LOC}
devices:
  - deviceId: sim-0001
"""


def run_cli(args: list[str], env: dict[str, str] | None = None):  # noqa: ANN201
    return CliRunner().invoke(app, args, env=env)


def test_cli_precedence(tmp_path: Path) -> None:
    cfg = write(tmp_path, FILE)
    base = ["run", "--config", str(cfg), "--dry-run"]
    only_file = run_cli(base)
    assert "file-host:9001" in only_file.output
    env = {"SIM_BROKER_HOST": "env-host", "SIM_BROKER_PORT": "9002"}
    with_env = run_cli(base, env)
    assert "env-host:9002" in with_env.output
    cli = run_cli([*base, "--host", "cli-host", "--port", "9003"], env)
    assert "cli-host:9003" in cli.output
    assert cli.exit_code == 0


def test_cli_location_id_override(tmp_path: Path) -> None:
    cfg = write(tmp_path, FILE)
    result = run_cli(["run", "--config", str(cfg), "--dry-run"], {"SIM_LOCATION_ID": LOC2})
    assert LOC2 in result.output and LOC not in result.output
    bad = run_cli(["run", "--config", str(cfg), "--dry-run", "--location-id", "zzz"])
    assert bad.exit_code != 0


def test_cli_dry_run_cert_report(tmp_path: Path) -> None:
    cfg = write(tmp_path, FILE)
    certs = tmp_path / "certs"
    certs.mkdir()
    (certs / "ca.crt").write_text("fake")
    result = run_cli(["run", "--config", str(cfg), "--cert-dir", str(certs), "--dry-run"])
    assert result.exit_code == 0
    assert "sim-0001.crt" in result.output and "MISSING" in result.output
    assert "ca.crt ok" in result.output


def test_cli_insecure_local_ignores_cert_dir(tmp_path: Path) -> None:
    cfg = write(tmp_path, FILE)
    result = run_cli(
        ["run", "--config", str(cfg), "--insecure-local", "--cert-dir", "nowhere", "--dry-run"]
    )
    assert result.exit_code == 0
    assert "ignored" in result.output and "MISSING" not in result.output


def test_cli_without_dry_run_exits_2(tmp_path: Path) -> None:
    result = run_cli(["run", "--config", str(write(tmp_path, FILE))])
    assert result.exit_code == 2 and "runtime not implemented yet" in result.output


def test_cli_bad_config_exit_1(tmp_path: Path) -> None:
    result = run_cli(["run", "--config", str(write(tmp_path, "broker:\n  verify: false\n"))])
    assert result.exit_code == 1


def test_no_verification_option_exists() -> None:
    result = run_cli(["run", "--help"])
    assert "verify" not in result.output.lower()
