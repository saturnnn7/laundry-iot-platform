<#
Runs tools/certs/certs.sh inside a pinned openssl container, so Windows needs only Docker.

Usage (from the repo root, PowerShell 7):
  pwsh -File tools/certs.ps1 ca
  pwsh -File tools/certs.ps1 server
  pwsh -File tools/certs.ps1 client --cn backend
  pwsh -File tools/certs.ps1 devices --count 3 --out infra/pki/devices

Arguments are passed to certs.sh unchanged. Paths are relative to the repo root.
#>
$ErrorActionPreference = 'Stop'

$image = 'alpine/openssl:3.5.4'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'docker not found in PATH'
}

docker run --rm `
    -v "${root}:/work" `
    -w /work `
    --entrypoint sh `
    $image `
    tools/certs/certs.sh @args

exit $LASTEXITCODE
