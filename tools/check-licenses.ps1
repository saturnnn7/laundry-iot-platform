<#
Checks the licenses of all NuGet packages (direct and transitive) of the backend solution.

Usage (from the repo root, PowerShell 7):
  pwsh ./tools/check-licenses.ps1

Rules:
  - the license is read from the package .nuspec in the global NuGet cache
  - allowed: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, PostgreSQL, ISC
    (combined with OR, AND and parentheses)
  - banned regardless of license: MediatR, AutoMapper, MassTransit (and MassTransit.*), FluentAssertions
  - any other package fails unless it is listed in tools/license-exceptions.txt (PackageId|reason)

The table is printed and, when GITHUB_STEP_SUMMARY is set, appended to that file.
Exit code 1 when any package fails.

Dry run: -InjectPackage 'Id|Version|License' adds a fake package (License is an expression,
no nuspec is read) to show how banned and unknown licenses are reported.
#>
[CmdletBinding()]
param(
    [string[]]$InjectPackage = @()
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'src/backend/Laundry.Backend.slnx'
$exceptionsPath = Join-Path $PSScriptRoot 'license-exceptions.txt'

$allowed = @('MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'PostgreSQL', 'ISC')
$bannedIds = @('MediatR', 'AutoMapper', 'FluentAssertions')

function Test-Banned([string]$Id) {
    return ($bannedIds -contains $Id) -or $Id.StartsWith('MassTransit.', [System.StringComparison]::OrdinalIgnoreCase) -or $Id -ieq 'MassTransit'
}

# Evaluates an SPDX-like expression: OR (lowest), AND, parentheses. Unknown ids and WITH are not allowed.
function Test-LicenseExpression([string]$Expression) {
    $tokens = [System.Collections.Generic.List[string]]::new()
    foreach ($m in [regex]::Matches($Expression, '\(|\)|[^\s()]+')) {
        $tokens.Add($m.Value)
    }

    $script:pos = 0

    function Read-Or {
        $result = Read-And
        while ($script:pos -lt $tokens.Count -and $tokens[$script:pos] -ieq 'OR') {
            $script:pos++
            $right = Read-And
            $result = $result -or $right
        }
        return $result
    }

    function Read-And {
        $result = Read-Atom
        while ($script:pos -lt $tokens.Count -and $tokens[$script:pos] -ieq 'AND') {
            $script:pos++
            $right = Read-Atom
            $result = $result -and $right
        }
        return $result
    }

    function Read-Atom {
        if ($script:pos -ge $tokens.Count) {
            throw 'Unexpected end of license expression.'
        }
        $token = $tokens[$script:pos]
        $script:pos++
        if ($token -eq '(') {
            $inner = Read-Or
            if ($script:pos -ge $tokens.Count -or $tokens[$script:pos] -ne ')') {
                throw 'Missing closing parenthesis in license expression.'
            }
            $script:pos++
            return $inner
        }
        if ($token -in @(')', 'AND', 'OR', 'WITH')) {
            throw "Unexpected token '$token' in license expression."
        }
        return ($allowed | Where-Object { $_ -ieq $token }).Count -gt 0
    }

    try {
        $value = Read-Or
        if ($script:pos -lt $tokens.Count) {
            return $false
        }
        return [bool]$value
    }
    catch {
        return $false
    }
}

# Returns @{ Text = <what to show>; Allowed = <bool> } from a nuspec file.
function Get-NuspecLicense([string]$NuspecPath) {
    [xml]$xml = Get-Content -LiteralPath $NuspecPath -Raw
    $meta = $xml.package.metadata
    $license = $meta.license
    $licenseUrl = [string]$meta.licenseUrl

    if ($null -ne $license) {
        $type = [string]$license.type
        $text = [string]$license.'#text'
        if ($type -eq 'expression') {
            return @{ Text = $text.Trim(); Allowed = (Test-LicenseExpression $text) }
        }
        if ($type -eq 'file') {
            return @{ Text = "file:$($text.Trim())"; Allowed = $false }
        }
    }

    if ($licenseUrl -match '^https://licenses\.nuget\.org/(?<expr>.+)$') {
        $expr = [uri]::UnescapeDataString($Matches['expr'])
        return @{ Text = $expr; Allowed = (Test-LicenseExpression $expr) }
    }

    if ($licenseUrl) {
        return @{ Text = "url:$licenseUrl"; Allowed = $false }
    }

    return @{ Text = '(none)'; Allowed = $false }
}

function Read-Exceptions([string]$Path) {
    $result = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $result
    }
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) {
            continue
        }
        $parts = $trimmed.Split('|', 2)
        $result[$parts[0].Trim().ToLowerInvariant()] = if ($parts.Count -gt 1) { $parts[1].Trim() } else { '' }
    }
    return $result
}

# Restore (cheap when up to date), then list packages.
& dotnet restore $solution --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

$listing = & dotnet list $solution package --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    throw "dotnet list package failed with exit code $LASTEXITCODE."
}
$data = ($listing -join "`n") | ConvertFrom-Json -Depth 20

$packages = @{}
foreach ($project in $data.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($p in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($null -eq $p) {
                continue
            }
            $packages["$($p.id)|$($p.resolvedVersion)"] = @{ Id = [string]$p.id; Version = [string]$p.resolvedVersion }
        }
    }
}

$cacheLine = (& dotnet nuget locals global-packages --list) -join "`n"
$cache = ($cacheLine -replace '^\s*global-packages:\s*', '').Trim()
if (-not (Test-Path -LiteralPath $cache)) {
    throw "The global NuGet cache '$cache' does not exist."
}

$exceptions = Read-Exceptions $exceptionsPath
$rows = [System.Collections.Generic.List[object]]::new()

foreach ($entry in $packages.Values) {
    $nuspec = Join-Path $cache "$($entry.Id.ToLowerInvariant())/$($entry.Version.ToLowerInvariant())/$($entry.Id.ToLowerInvariant()).nuspec"
    if (Test-Path -LiteralPath $nuspec) {
        $info = Get-NuspecLicense $nuspec
    }
    else {
        $info = @{ Text = '(nuspec not found)'; Allowed = $false }
    }
    $rows.Add([pscustomobject]@{ Id = $entry.Id; Version = $entry.Version; License = $info.Text; Allowed = $info.Allowed })
}

foreach ($spec in $InjectPackage) {
    $parts = $spec.Split('|', 3)
    if ($parts.Count -ne 3) {
        throw "Invalid -InjectPackage '$spec'. Use Id|Version|License."
    }
    $rows.Add([pscustomobject]@{ Id = $parts[0]; Version = $parts[1]; License = $parts[2]; Allowed = (Test-LicenseExpression $parts[2]) })
}

$table = foreach ($row in ($rows | Sort-Object -Property Id, Version)) {
    if (Test-Banned $row.Id) {
        $status = 'FAIL (banned)'
    }
    elseif ($row.Allowed) {
        $status = 'ok'
    }
    elseif ($exceptions.ContainsKey($row.Id.ToLowerInvariant())) {
        $status = "exception ($($exceptions[$row.Id.ToLowerInvariant()]))"
    }
    else {
        $status = 'FAIL (license not allowed)'
    }
    [pscustomobject]@{ Id = $row.Id; Version = $row.Version; License = $row.License; Status = $status }
}

$table | Format-Table -AutoSize | Out-String -Width 250 | Write-Host

$failed = @($table | Where-Object { $_.Status.StartsWith('FAIL') })

if ($env:GITHUB_STEP_SUMMARY) {
    $md = [System.Collections.Generic.List[string]]::new()
    $md.Add('## NuGet licenses')
    $md.Add('')
    $md.Add('| Package | Version | License | Status |')
    $md.Add('|---|---|---|---|')
    foreach ($row in $table) {
        $md.Add("| $($row.Id) | $($row.Version) | $($row.License) | $($row.Status) |")
    }
    $md.Add('')
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $md
}

Write-Host "$($table.Count) packages checked, $($failed.Count) failed."
if ($failed.Count -gt 0) {
    Write-Host 'Failed packages:'
    foreach ($row in $failed) {
        Write-Host "  $($row.Id) $($row.Version): $($row.License) -> $($row.Status)"
    }
    exit 1
}
exit 0
