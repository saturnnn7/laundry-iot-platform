<#
Checks the broker rules: mutual TLS and the per-device ACL.

Usage (from the repo root, PowerShell 7, Docker):
  pwsh -File infra/compose/tests/mqtt-acl.ps1
  pwsh -File infra/compose/tests/mqtt-acl.ps1 -MqttHost broker.example.com -Port 8883

With the default -MqttHost (mosquitto) the test clients join the compose network.
Any other host is reached over the normal Docker network, so a remote broker can be tested too.
Exit code is 1 when any check fails.

Note: the broker accepts every SUBSCRIBE and applies the read rules when it delivers messages,
so a denied subscription shows up as "no message delivered", not as a failed SUBACK.
#>
[CmdletBinding()]
param(
    [string]$MqttHost = 'mosquitto',
    [int]$Port = 8883,
    [string]$CertDir = 'infra/pki/devices',
    [string]$BackendCertDir = 'infra/compose/certs'
)

$ErrorActionPreference = 'Stop'

$image = 'eclipse-mosquitto:2.0.22'
$network = 'laundry_net'
$location = '11111111-1111-4111-8111-111111111111'
$run = [guid]::NewGuid().ToString('N').Substring(0, 8)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path

function Get-AbsPath([string]$p) {
    if ([IO.Path]::IsPathRooted($p)) { return [IO.Path]::GetFullPath($p) }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $p))
}

function Get-RelPath([string]$abs) {
    return [IO.Path]::GetRelativePath($repoRoot, $abs).Replace('\', '/')
}

function Invoke-Certs([string[]]$CertArgs) {
    & pwsh -NoProfile -File (Join-Path $repoRoot 'tools/certs.ps1') @CertArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "certs.ps1 failed: $CertArgs" }
}

$deviceDir = Get-AbsPath $CertDir
$backendDir = Get-AbsPath $BackendCertDir
$throwawayRoot = Get-AbsPath 'infra/pki/throwaway'
$throwawayDevices = Join-Path $throwawayRoot 'devices'

# Identity = a certificate directory and a name; $null means no client certificate.
function New-Identity([string]$Dir, [string]$Name) { return @{ Dir = $Dir; Name = $Name } }

function Get-DockerArgs($Identity, [string]$CaDir, [string[]]$Tool, [bool]$Detached, [string]$Name, [string]$Version = 'mqttv5') {
    $a = @('run')
    # -t gives the client a pseudo terminal, so its output is line-buffered and the SUBACK can be seen while it runs
    if ($Detached) { $a += @('-d', '-t', '--name', $Name) } else { $a += '--rm' }
    $a += @('--user', '0')
    if ($MqttHost -eq 'mosquitto') { $a += @('--network', $network) }
    $a += @('-v', "${CaDir}:/ca:ro")
    if ($Identity) { $a += @('-v', "$($Identity.Dir):/id:ro") }
    $a += @('--entrypoint', 'timeout', $image, '30')
    $a += $Tool
    $a += @('--cafile', '/ca/ca.crt', '-h', $MqttHost, '-p', "$Port", '-V', $Version, '-q', '1')
    if ($Identity) { $a += @('--cert', "/id/$($Identity.Name).crt", '--key', "/id/$($Identity.Name).key") }
    return $a
}

# An empty payload sends a null message; with -Retain that clears a retained message.
function Invoke-Pub($Identity, [string]$Topic, [string]$Payload, [string]$CaDir = $deviceDir, [bool]$Retain = $false, [string]$Version = 'mqttv5') {
    $a = (Get-DockerArgs $Identity $CaDir @('mosquitto_pub') $false '' $Version) + @('-t', $Topic)
    if ($Payload) { $a += @('-m', $Payload) } else { $a += '-n' }
    if ($Retain) { $a += '-r' }
    $out = (& docker @a 2>&1 | Out-String).Trim()
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $out }
}

$script:containers = @()

# Starts a subscriber in the background and waits until the broker has answered the SUBSCRIBE.
function Start-Sub($Identity, [string[]]$Topics, [int]$Wait, [int]$Count = 0, [string]$CaDir = $deviceDir) {
    $name = "mqtt-acl-$run-$($script:containers.Count)"
    $tool = @('mosquitto_sub')
    $a = Get-DockerArgs $Identity $CaDir $tool $true $name
    foreach ($t in $Topics) { $a += @('-t', $t) }
    $a += @('-F', '%t %p', '-W', "$Wait", '-d')
    if ($Count -gt 0) { $a += @('-C', "$Count") }
    & docker @a | Out-Null
    $script:containers += $name
    for ($i = 0; $i -lt 60; $i++) {
        $logs = (& docker logs $name 2>&1 | Out-String)
        if ($logs -match 'received SUBACK') { return $name }
        Start-Sleep -Milliseconds 250
    }
    throw "subscriber $name did not get a SUBACK in time:`n$logs"
}

function Receive-Sub([string]$Name) {
    & docker wait $Name | Out-Null
    return (& docker logs $Name 2>&1 | Out-String) -replace "`r", ''
}

# Lines "topic payload" delivered to a subscriber (debug lines start with "Client").
function Get-Messages([string]$Logs) {
    return @($Logs -split "`n" | Where-Object { $_ -match '^v1/' })
}

$failed = 0
function Write-Result([string]$Name, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) { Write-Host "PASS  $Name" -ForegroundColor Green }
    else { Write-Host "FAIL  $Name" -ForegroundColor Red; $script:failed++ }
    if ($Detail) { ($Detail -split "`n") | ForEach-Object { Write-Host "        $_" } }
}

function Test-Accepted($r) { return ($r.ExitCode -eq 0 -and $r.Output -notmatch 'failed|Error') }
function Test-NotAuthorized($r) { return ($r.Output -match 'Not authorized') }

try {
    # Certificates for the two simulated devices (created when missing).
    $caKey = Get-AbsPath 'infra/pki/ca/ca.key'
    foreach ($n in 'sim-0001', 'sim-0002') {
        if (-not (Test-Path (Join-Path $deviceDir "$n.crt"))) {
            if (-not (Test-Path $caKey)) { throw 'CA not found: run "pwsh -File tools/certs.ps1 ca" first' }
            Invoke-Certs @('devices', '--count', '2', '--out', (Get-RelPath $deviceDir))
            break
        }
    }
    foreach ($f in 'backend.crt', 'backend.key', 'ca.crt') {
        if (-not (Test-Path (Join-Path $backendDir $f))) { throw "missing $f in $backendDir" }
    }

    $dev1 = New-Identity $deviceDir 'sim-0001'
    $dev2 = New-Identity $deviceDir 'sim-0002'
    $backend = New-Identity $backendDir 'backend'

    Write-Host "Broker: ${MqttHost}:$Port   run id: $run"
    Write-Host ''

    # a. no client certificate
    $r = Invoke-Pub $null "v1/$location/sim-0001/telemetry" "a-$run"
    Write-Result 'a. no client certificate: connection refused' ($r.ExitCode -ne 0) "exit=$($r.ExitCode)`n$($r.Output)"

    # b. certificate from another CA
    if (Test-Path $throwawayRoot) { Remove-Item $throwawayRoot -Recurse -Force }
    Invoke-Certs @('ca', '--ca-dir', (Get-RelPath (Join-Path $throwawayRoot 'ca')))
    Invoke-Certs @('client', '--ca-dir', (Get-RelPath (Join-Path $throwawayRoot 'ca')), '--out', (Get-RelPath $throwawayDevices), '--cn', 'sim-0001')
    $rogue = New-Identity $throwawayDevices 'sim-0001'
    $r = Invoke-Pub $rogue "v1/$location/sim-0001/telemetry" "b-$run"
    Write-Result 'b. certificate from a different CA: refused' ($r.ExitCode -ne 0) "exit=$($r.ExitCode)`n$($r.Output)"

    # c. sim-0001 publishes to its own topics, the backend receives each
    $own = [ordered]@{
        "v1/$location/sim-0001/telemetry" = "c-telemetry-$run"
        "v1/$location/sim-0001/event"     = "c-event-$run"
        "v1/$location/sim-0001/status"    = "c-status-$run"
        "v1/$location/sim-0001/cmd/ack"   = "c-ack-$run"
    }
    $sub = Start-Sub $backend @('v1/+/+/telemetry', 'v1/+/+/event', 'v1/+/+/status', 'v1/+/+/cmd/ack') 20 4 $backendDir
    $accepted = $true
    $detail = @()
    foreach ($t in $own.Keys) {
        $r = Invoke-Pub $dev1 $t $own[$t]
        if (-not (Test-Accepted $r)) { $accepted = $false; $detail += "$t -> exit=$($r.ExitCode) $($r.Output)" }
    }
    $msgs = Get-Messages (Receive-Sub $sub)
    $missing = @($own.Keys | Where-Object { $msgs -notcontains "$_ $($own[$_])" })
    if ($missing) { $detail += "backend did not receive: $($missing -join ', ')" }
    Write-Result 'c. sim-0001 publishes to its own topics; backend receives all four' ($accepted -and -not $missing) ($detail -join "`n")

    # d. sim-0001 publishes to sim-0002 telemetry
    $payload = "d-$run"
    $sub = Start-Sub $backend @('v1/+/+/telemetry') 4 0 $backendDir
    $r = Invoke-Pub $dev1 "v1/$location/sim-0002/telemetry" $payload
    $msgs = Get-Messages (Receive-Sub $sub)
    $leaked = @($msgs | Where-Object { $_ -match [regex]::Escape($payload) })
    Write-Result "d. sim-0001 -> sim-0002 telemetry: not authorized, backend receives nothing" ((Test-NotAuthorized $r) -and -not $leaked) "exit=$($r.ExitCode)`n$($r.Output)"

    # e. sim-0001 publishes to a cmd topic
    $r = Invoke-Pub $dev1 "v1/$location/sim-0001/cmd" "e-$run"
    Write-Result 'e. sim-0001 -> cmd topic: not authorized' (Test-NotAuthorized $r) "exit=$($r.ExitCode)`n$($r.Output)"

    # f. sim-0001 subscribes outside its own topics: it must receive nothing.
    # Positive control: the same two messages are delivered to subscribers that may read them
    # (the backend for telemetry, sim-0002 for its own cmd; the backend has no read rule on cmd).
    $cmdTopic = "v1/$location/sim-0002/cmd"
    $telTopic = "v1/$location/sim-0002/telemetry"
    $fCmd = "f-cmd-$run"
    $fTel = "f-telemetry-$run"
    $sub = Start-Sub $dev1 @($cmdTopic, 'v1/+/+/telemetry') 8 0
    $ctlBackend = Start-Sub $backend @('v1/+/+/telemetry') 8 0 $backendDir
    $ctlDev2 = Start-Sub $dev2 @($cmdTopic) 8 0
    $rc = Invoke-Pub $backend $cmdTopic $fCmd $backendDir
    $rt = Invoke-Pub $dev2 $telTopic $fTel
    $logs = Receive-Sub $sub
    $msgs = Get-Messages $logs
    $gotBackend = Get-Messages (Receive-Sub $ctlBackend)
    $gotDev2 = Get-Messages (Receive-Sub $ctlDev2)
    $controlOk = (Test-Accepted $rc) -and (Test-Accepted $rt) -and
        ($gotBackend -contains "$telTopic $fTel") -and ($gotDev2 -contains "$cmdTopic $fCmd")
    $shown = ($logs -split "`n" | Where-Object { $_ -match 'SUBSCRIBE|SUBACK|Subscribed' }) -join "`n"
    Write-Result 'f. sim-0001 subscribes to sim-0002 cmd and v1/+/+/telemetry: nothing delivered' (($msgs.Count -eq 0) -and $controlOk) ("$shown`nmessages delivered to sim-0001: $($msgs.Count)`ncontrol: backend received telemetry: $($gotBackend -contains "$telTopic $fTel"), sim-0002 received cmd: $($gotDev2 -contains "$cmdTopic $fCmd")")

    # g. backend sends a cmd to sim-0001: sim-0001 receives it, sim-0002 does not
    $payload = "g-$run"
    $sub1 = Start-Sub $dev1 @("v1/$location/sim-0001/cmd") 10 1
    $sub2 = Start-Sub $dev2 @("v1/$location/sim-0002/cmd") 5 0
    $r = Invoke-Pub $backend "v1/$location/sim-0001/cmd" $payload $backendDir
    $m1 = Get-Messages (Receive-Sub $sub1)
    $m2 = Get-Messages (Receive-Sub $sub2)
    $ok = (Test-Accepted $r) -and ($m1 -contains "v1/$location/sim-0001/cmd $payload") -and ($m2.Count -eq 0)
    Write-Result 'g. backend cmd for sim-0001: sim-0001 receives it, sim-0002 does not' $ok "sim-0001 received: $($m1.Count), sim-0002 received: $($m2.Count)"

    # h. backend subscribes to v1/+/+/telemetry and sees both devices
    $sub = Start-Sub $backend @('v1/+/+/telemetry') 15 2 $backendDir
    $null = Invoke-Pub $dev1 "v1/$location/sim-0001/telemetry" "h1-$run"
    $null = Invoke-Pub $dev2 "v1/$location/sim-0002/telemetry" "h2-$run"
    $msgs = Get-Messages (Receive-Sub $sub)
    $ok = ($msgs -contains "v1/$location/sim-0001/telemetry h1-$run") -and ($msgs -contains "v1/$location/sim-0002/telemetry h2-$run")
    Write-Result 'h. backend receives telemetry from both devices' $ok "received: $($msgs.Count)"

    # i. a 400-byte payload is accepted and delivered (max_packet_size is 8192)
    $topic = "v1/$location/sim-0001/telemetry"
    $payload = "i-$run-" + ('x' * 389)
    $sub = Start-Sub $backend @('v1/+/+/telemetry') 10 1 $backendDir
    $r = Invoke-Pub $dev1 $topic $payload
    $msgs = Get-Messages (Receive-Sub $sub)
    Write-Result "i. $($payload.Length)-byte payload: accepted and delivered" ((Test-Accepted $r) -and ($msgs -contains "$topic $payload")) "exit=$($r.ExitCode)"

    # j. a payload above the packet size limit is not delivered.
    # Sent as MQTT 3.1.1: an MQTT 5 client reads the advertised maximum packet size and never sends the packet.
    $payload = "j-$run-" + ('x' * 9000)
    $sub = Start-Sub $backend @('v1/+/+/telemetry') 5 0 $backendDir
    $r = Invoke-Pub $dev1 $topic $payload $deviceDir $false 'mqttv311'
    $msgs = Get-Messages (Receive-Sub $sub)
    $leaked = @($msgs | Where-Object { $_ -match "j-$run" })
    $out = if ($r.Output.Length -gt 300) { $r.Output.Substring(0, 300) } else { $r.Output }
    Write-Result "j. $($payload.Length)-byte payload: not delivered" (-not $leaked) "exit=$($r.ExitCode)`n$out"

    # k. a retained status message is delivered to the backend but not to another device
    $statusTopic = "v1/$location/sim-0002/status"
    $payload = "k-$run"
    $r = Invoke-Pub $dev2 $statusTopic $payload $deviceDir $true
    $subDev = Start-Sub $dev1 @('v1/+/+/status') 5 0
    $subBackend = Start-Sub $backend @('v1/+/+/status') 10 0 $backendDir
    $gotDev1 = Get-Messages (Receive-Sub $subDev)
    $gotBackend = Get-Messages (Receive-Sub $subBackend)
    $rc = Invoke-Pub $dev2 $statusTopic '' $deviceDir $true
    $subAfter = Start-Sub $backend @('v1/+/+/status') 4 0 $backendDir
    $after = Get-Messages (Receive-Sub $subAfter)
    $okDev = -not @($gotDev1 | Where-Object { $_ -match "k-$run" })
    $okBackend = $gotBackend -contains "$statusTopic $payload"
    $okCleared = -not @($after | Where-Object { $_ -match "k-$run" })
    Write-Result 'k. retained status: backend receives it, sim-0001 does not; cleared afterwards' ((Test-Accepted $r) -and $okDev -and $okBackend -and (Test-Accepted $rc) -and $okCleared) "sim-0001 got it: $(-not $okDev), backend got it: $okBackend, still retained after clear: $(-not $okCleared)"
}
finally {
    # make sure no retained test message is left behind
    if ($dev2) { try { $null = Invoke-Pub $dev2 "v1/$location/sim-0002/status" '' $deviceDir $true } catch { } }
    foreach ($c in $script:containers) { & docker rm -f $c 2>&1 | Out-Null }
    if (Test-Path $throwawayRoot) { Remove-Item $throwawayRoot -Recurse -Force }
}

Write-Host ''
if ($failed -gt 0) {
    Write-Host "$failed check(s) FAILED" -ForegroundColor Red
    exit 1
}
Write-Host 'All checks passed' -ForegroundColor Green
exit 0
