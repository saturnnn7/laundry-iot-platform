"""Command line entry point."""

from __future__ import annotations

from pathlib import Path
from typing import Annotated

import typer

from laundry_sim.certs import cert_paths
from laundry_sim.config import ConfigError, load_config
from laundry_sim.devices import Device, generate_devices, resolve_devices
from laundry_sim.ids import check_guid
from laundry_sim.settings import Settings, resolve_settings

app = typer.Typer(add_completion=False, no_args_is_help=True, help="Laundry device simulator.")


def _guid_option(value: str | None) -> str | None:
    if value is None:
        return None
    try:
        return check_guid(value)
    except ValueError as exc:
        raise typer.BadParameter(str(exc)) from exc


def _print_plan(devices: list[Device], settings: Settings) -> None:
    mode = "tls" if settings.tls else "plain (insecure-local)"
    typer.echo(f"Broker: {settings.host}:{settings.port} {mode}")
    if settings.cert_dir_ignored:
        typer.echo("note: certificate directory is ignored with --insecure-local")
    typer.echo(f"Devices: {len(devices)}")
    for dev in devices:
        typer.echo(f"  {dev.device_id}  {dev.kind}  {dev.location_id}  fw {dev.firmware}")
    if settings.tls and settings.cert_dir is None:
        typer.echo("Certificate directory: not set (use --cert-dir or SIM_CERT_DIR)")
    if settings.cert_dir is not None:
        typer.echo(f"Certificate directory: {settings.cert_dir}")
        reported: set[Path] = set()
        for dev in devices:
            paths = cert_paths(dev.device_id, settings.cert_dir)
            for path in (paths.ca, paths.cert, paths.key):
                if path in reported:
                    continue
                reported.add(path)
                state = "MISSING" if path in paths.missing else "ok"
                typer.echo(f"  {path} {state}")


@app.callback()
def main() -> None:
    """Laundry device simulator."""


@app.command()
def run(
    config: Annotated[Path, typer.Option("--config", help="YAML config file.")],
    count: Annotated[
        int | None, typer.Option("--count", min=1, help="Generate sim-0001..sim-NNNN.")
    ] = None,
    insecure_local: Annotated[
        bool, typer.Option("--insecure-local", help="Plain MQTT on a local broker, no TLS.")
    ] = False,
    time_scale: Annotated[
        float, typer.Option("--time-scale", min=0.001, help="Speed up simulated time.")
    ] = 1.0,
    seed: Annotated[int | None, typer.Option("--seed", help="Random seed.")] = None,
    host: Annotated[
        str | None, typer.Option("--host", envvar="SIM_BROKER_HOST", help="Broker host.")
    ] = None,
    port: Annotated[
        int | None,
        typer.Option("--port", envvar="SIM_BROKER_PORT", min=1, max=65535, help="Broker port."),
    ] = None,
    cert_dir: Annotated[
        Path | None,
        typer.Option("--cert-dir", envvar="SIM_CERT_DIR", help="Directory with ca.crt and keys."),
    ] = None,
    location_id: Annotated[
        str | None,
        typer.Option(
            "--location-id",
            envvar="SIM_LOCATION_ID",
            callback=_guid_option,
            help="Location GUID for all devices.",
        ),
    ] = None,
    dry_run: Annotated[
        bool, typer.Option("--dry-run", help="Validate and print the plan, then exit.")
    ] = False,
) -> None:
    """Run simulated devices."""
    del time_scale, seed  # used by the runtime in a later task
    try:
        cfg = load_config(config)
        settings = resolve_settings(
            cfg,
            config.resolve().parent,
            host=host,
            port=port,
            cert_dir=cert_dir,
            insecure_local=insecure_local,
        )
        if count is not None:
            devices = generate_devices(count, cfg.defaults, location_id)
        else:
            devices = resolve_devices(cfg.devices, cfg.defaults, location_id)
        if not devices:
            raise ConfigError("no devices: list them in the config or pass --count")
    except ConfigError as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(1) from exc

    _print_plan(devices, settings)
    if dry_run:
        raise typer.Exit(0)
    typer.echo("runtime not implemented yet")
    raise typer.Exit(2)
