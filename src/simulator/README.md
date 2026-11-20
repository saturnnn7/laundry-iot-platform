# Laundry device simulator

Simulated washers and dryers that speak the MQTT v1 contract (`docs/api/contracts`). Runtime is not implemented yet; `run` validates the config and prints the plan.

## Install and test

```
uv sync
uv run ruff check .
uv run pytest -q
```

## Dry run

```
uv run sim run --config devices.example.yaml --insecure-local --dry-run
uv run sim run --config devices.example.yaml --count 3 --cert-dir ../../infra/pki/devices --dry-run
```

Missing certificate files are printed as `MISSING`. Without `--dry-run` the command exits with code 2.

## Settings

Order: command line, environment, config file, default.

| Option | Environment | Default |
|---|---|---|
| `--host` | `SIM_BROKER_HOST` | `localhost` |
| `--port` | `SIM_BROKER_PORT` | 8883 (1883 with `--insecure-local`) |
| `--cert-dir` | `SIM_CERT_DIR` | none |
| `--location-id` | `SIM_LOCATION_ID` | `defaults.locationId` |

Certificates come from `tools/certs` (see `tools/certs/README.md`). There is no option to turn off certificate or host name checks.
