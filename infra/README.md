# Infrastructure

Docker Compose stack for local development. The same files are meant to run on the cloud VM later.

## Services

All files live in `infra/compose/`.

| Service | Purpose |
|---|---|
| `mosquitto` | MQTT broker (Eclipse Mosquitto 2.0). Only MQTTS on 8883, client certificate required, user = certificate CN, ACL per device (`mosquitto/acl`). |
| `postgres` | PostgreSQL 17. No published port. Data in a named volume. |
| `backend` | Placeholder (`traefik/whoami`) listening on 8080: answers 200 on every path and echoes the request headers. Replaced by the real image, see below. |
| `caddy` | Reverse proxy and HTTPS entry point, fixed address `172.30.0.10` on the compose network. Forwards to `backend:8080`. |

Network: one bridge network `laundry_net`, subnet `172.30.0.0/24`.

## Quick start (local)

Run every command from the repo root, in PowerShell 7 with Docker.

1. Create the certificates (details in `tools/certs/README.md`):

   ```
   pwsh -File tools/certs.ps1 ca
   pwsh -File tools/certs.ps1 server
   pwsh -File tools/certs.ps1 client --cn backend
   ```

2. Create the env file and set the secrets:

   ```
   Copy-Item infra/compose/.env.example infra/compose/.env
   ```

   Generate a database password and a signing key (hex strings, safe inside a connection string) and paste them into `infra/compose/.env` in place of the `CHANGE_ME` values:

   ```
   [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))   # POSTGRES_PASSWORD
   [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))   # JWT_SIGNING_KEY
   ```

   `BACKEND_IMAGE` is only needed for the real backend. `infra/compose/.env` is ignored by git.

3. Start the stack:

   ```
   docker compose -f infra/compose/docker-compose.yml up -d
   docker compose -f infra/compose/docker-compose.yml ps
   ```

4. Check the broker rules (creates `sim-0001` and `sim-0002` certificates if missing):

   ```
   pwsh -File infra/compose/tests/mqtt-acl.ps1
   ```

   The script exits with 1 if any check fails. To test a remote broker, pass `-MqttHost <name> -Port <port>`; the host name must be in the server certificate SAN.

5. Check HTTPS through Caddy (`-k` because the local certificate comes from Caddy's internal CA):

   ```
   curl.exe -k -i https://localhost:8443/health
   curl.exe -k -H "X-Forwarded-For: 9.9.9.9" https://localhost:8443/anything
   ```

   The placeholder echoes the headers: `X-Forwarded-For` shows the address Caddy saw, not the forged value, and `X-Forwarded-Proto` is `https`. Plain HTTP is on `http://localhost:8080`; it redirects to `https://localhost/...` without the local port 8443, so use the HTTPS URL directly when testing locally.

6. Stop. `down -v` also deletes the volumes (database, broker state, Caddy certificates):

   ```
   docker compose -f infra/compose/docker-compose.yml down
   docker compose -f infra/compose/docker-compose.yml down -v
   ```

Ports can be changed with `HTTP_PORT`, `HTTPS_PORT` and `MQTT_PORT` in `.env`.

## Switch to the real backend

Set `BACKEND_IMAGE` and `JWT_SIGNING_KEY` in `infra/compose/.env`, then add the override file to every compose command:

```
docker compose -f infra/compose/docker-compose.yml -f infra/compose/backend.compose.yml up -d
```

The override replaces the image, adds the configuration (connection string, JWT key, forwarded headers with Caddy as the only known proxy) and a healthcheck on `/health/live`. The backend waits for healthy PostgreSQL and Mosquitto. The MQTT connection settings of the backend are not defined yet; the override has a comment where they go.

The healthcheck is a bash probe, because the image has no `curl`. It opens a TCP connection and expects a `200` status line. The same probe from outside, against the running placeholder or backend (the service name `backend` resolves on the compose network; `/health` also checks PostgreSQL, `/health/live` does not):

```
docker run --rm --network laundry_net mcr.microsoft.com/dotnet/aspnet:10.0 bash -c "exec 3<>/dev/tcp/backend/8080 && printf 'GET /health HTTP/1.1\r\nHost: backend\r\nConnection: close\r\n\r\n' >&3 && head -n1 <&3"
```

Expected output: `HTTP/1.1 200 OK`. The line works in PowerShell and in bash.

## Local backend outside Docker

To run the backend from the IDE against the compose PostgreSQL, publish the database on loopback only:

```
docker compose -f infra/compose/docker-compose.yml -f infra/compose/dev.compose.yml up -d
```

This is for development only and is not used in the cloud.

## Notes

- Mosquitto reads its config and certificates from read-only mounts and installs copies with the owner and mode it needs when the container starts. The key on the host can stay mode 600 and owned by the host user: the container copies it as root. This also holds on a Linux host (rootless Docker is the exception: there the key must be readable by the user the daemon runs as).
- The broker accepts every SUBSCRIBE and applies the ACL when it delivers messages. A device that subscribes to another device's topics gets a normal SUBACK but never receives anything. Publishing outside the ACL is refused with `Not authorized` (MQTT 5).
- Packets are limited to 8192 bytes (`max_packet_size`); a larger publish is refused and the client is disconnected.
- The broker prints one TLS error line every 30 seconds: that is the container healthcheck opening a plain TCP connection.
- Caddy uses its own certificate authority for `localhost`. For a public name, set `DOMAIN` and open ports 80 and 443; Caddy obtains the certificate itself.
