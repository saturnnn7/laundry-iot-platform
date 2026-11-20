# Certificate tools

Creates a private CA, the broker server certificate, the backend client certificate and device certificates for MQTT over mutual TLS. Keys are EC (prime256v1), signatures use SHA-256, serial numbers are random.

Generated files go to `infra/pki/` and `infra/compose/certs/`. Both are ignored by git. Never commit keys or certificates.

Relative paths are always resolved from the repo root, whatever the current directory is.

## Run

- Linux or macOS: `sh tools/certs/certs.sh <subcommand> [options]` (needs only `openssl`).
- Windows: `pwsh -File tools/certs.ps1 <subcommand> [options]` (PowerShell 7 and Docker; the script runs `certs.sh` inside the pinned `alpine/openssl` image with the repo mounted).

The options are identical on both. Examples below use the Windows form; on Linux replace `pwsh -File tools/certs.ps1` with `sh tools/certs/certs.sh`.

## Subcommands

An existing key or certificate is never overwritten. It is skipped with a message; pass `--force` to replace it.

### ca

```
pwsh -File tools/certs.ps1 ca [--ca-dir DIR] [--days 3650] [--force]
```

Writes `DIR/ca.key` and `DIR/ca.crt`. Default `DIR` is `infra/pki/ca`. The CA has `CA:TRUE, pathlen:0` and key usage `keyCertSign, cRLSign`.

### server

```
pwsh -File tools/certs.ps1 server [--ca-dir DIR] [--out DIR] [--san "DNS:localhost,DNS:mosquitto,IP:127.0.0.1"] [--days 365] [--force]
```

Writes `server.key` and `server.crt` (CN = `broker`, EKU `serverAuth`) and copies `ca.crt` into `--out`. Default `--out` is `infra/compose/certs`. Clients check the SAN list and ignore the CN, so list every name or address the broker is reached by (for the cloud VM add its DNS name and public IP).

### client

```
pwsh -File tools/certs.ps1 client --cn backend [--ca-dir DIR] [--out DIR] [--days 365] [--force]
```

Writes `NAME.key` and `NAME.crt` (EKU `clientAuth`). Default `--out` is `infra/compose/certs`. The CN must match `^[A-Za-z0-9_-]{3,64}$`. The broker ACL treats the CN `backend` as the backend user.

### devices

```
pwsh -File tools/certs.ps1 devices --count N [--ca-dir DIR] [--out DIR] [--prefix sim] [--start 1] [--days 365] [--force]
```

Writes `<prefix>-NNNN.key` and `<prefix>-NNNN.crt` (4 digits, zero padded, EKU `clientAuth`) and copies `ca.crt`. Default `--out` is `infra/pki/devices`. Layout:

```
infra/pki/devices/
  ca.crt
  sim-0001.crt
  sim-0001.key
  sim-0002.crt
  ...
```

The device id (the CN) must match `^[A-Za-z0-9_-]{3,64}$` and the number must stay within 0001..9999. The CN `backend` is refused.

## Typical run from scratch

```
pwsh -File tools/certs.ps1 ca
pwsh -File tools/certs.ps1 server
pwsh -File tools/certs.ps1 client --cn backend
pwsh -File tools/certs.ps1 devices --count 3
```

## Inspect and verify

```
openssl x509 -in infra/compose/certs/server.crt -noout -subject -issuer -dates
openssl x509 -in infra/compose/certs/server.crt -noout -ext subjectAltName,extendedKeyUsage,keyUsage,basicConstraints
openssl x509 -in infra/pki/devices/sim-0001.crt -noout -text
openssl verify -CAfile infra/compose/certs/ca.crt infra/compose/certs/server.crt infra/compose/certs/backend.crt
openssl verify -CAfile infra/pki/devices/ca.crt infra/pki/devices/sim-0001.crt
```

On Windows without a local `openssl`, run the same commands through Docker:

```
docker run --rm -v "${PWD}:/work" -w /work alpine/openssl:3.5.4 x509 -in infra/pki/devices/sim-0001.crt -noout -subject -ext extendedKeyUsage
```

## Notes

- Private keys are not encrypted. This keeps the broker and simulator start-up free of passphrases, and it is acceptable for a development and demo setup. Anyone who can read a key file can act as that identity, so keep `infra/pki/` and `infra/compose/certs/` off shared storage. Key files get mode 600 on Linux.
- The CA key (`infra/pki/ca/ca.key`) signs everything. Keep it only where certificates are issued; the broker needs `ca.crt`, never `ca.key`.
- Certificate revocation is not set up. To retire an identity, remove it from the broker ACL or re-issue the CA.
