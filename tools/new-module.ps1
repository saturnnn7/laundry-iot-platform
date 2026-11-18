<#
Creates a new backend module from tools/module-template.

Usage (from the repo root, PowerShell 7):
  pwsh ./tools/new-module.ps1 -Name Booking
  pwsh ./tools/new-module.ps1 -Name Booking -SkipMigration

What it does:
  - renders the template into src/backend/src/Modules/<Name>
  - adds the five projects to Laundry.Backend.slnx
  - adds a Host reference to <Name>.Api
  - inserts services.Add<Name>Module(connectionString); into ModuleRegistry.cs
  - builds the Api project
  - creates the Initial migration (unless -SkipMigration)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$', Options = 'None')]
    [string]$Name,

    [switch]$SkipMigration
)

$ErrorActionPreference = 'Stop'

function Assert-LastExitCode([string]$What) {
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code $LASTEXITCODE."
    }
}

function Write-TextFile([string]$Path, [string]$Text) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $repoRoot 'src/backend'
$template = Join-Path $PSScriptRoot 'module-template'
$moduleDir = Join-Path $backend "src/Modules/$Name"
$registryPath = Join-Path $backend 'src/Host/ModuleRegistry.cs'
$lower = $Name.ToLowerInvariant()
$registryLine = "services.Add${Name}Module(connectionString);"

# Preflight: nothing is changed until all checks pass.
if (Test-Path $moduleDir) {
    throw "Module '$Name' already exists at $moduleDir. Nothing was changed."
}

$registryRaw = [System.IO.File]::ReadAllText($registryPath)
if ($registryRaw.Contains($registryLine)) {
    throw "ModuleRegistry.cs already contains '$registryLine'. Nothing was changed."
}
if ($registryRaw -notmatch '(?m)^[ \t]*// modules:begin[ \t]*\r?$' -or $registryRaw -notmatch '(?m)^[ \t]*// modules:end[ \t]*\r?$') {
    throw 'ModuleRegistry.cs has no "// modules:begin" and "// modules:end" markers. Nothing was changed.'
}

# Render the template.
$templateRoot = (Resolve-Path $template).Path
$files = Get-ChildItem -Path $templateRoot -Recurse -File -Filter '*.tmpl'
foreach ($file in $files) {
    $relative = $file.FullName.Substring($templateRoot.Length).TrimStart('\', '/')
    $relative = $relative.Substring(0, $relative.Length - '.tmpl'.Length)
    $relative = $relative.Replace('__Module__', $Name).Replace('__module__', $lower)

    $target = Join-Path $moduleDir $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null

    $text = [System.IO.File]::ReadAllText($file.FullName)
    $text = $text.Replace('__Module__', $Name).Replace('__module__', $lower)
    $text = $text.Replace("`r`n", "`n")
    Write-TextFile $target $text
}

Push-Location $backend
try {
    # Solution and Host reference.
    $layers = 'Contracts', 'Domain', 'Application', 'Infrastructure', 'Api'
    $projects = $layers | ForEach-Object { "src/Modules/$Name/$Name.$_/$Name.$_.csproj" }

    dotnet sln Laundry.Backend.slnx add --solution-folder "src/Modules/$Name" @projects
    Assert-LastExitCode 'dotnet sln add'

    dotnet add src/Host/Host.csproj reference "src/Modules/$Name/$Name.Api/$Name.Api.csproj"
    Assert-LastExitCode 'dotnet add reference'

    # Registry line, indented like the end marker.
    $newline = if ($registryRaw.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = [System.Collections.Generic.List[string]]($registryRaw -split "`r?`n")
    $endIndex = $lines.FindIndex({ param($l) $l.Trim() -eq '// modules:end' })
    $indent = $lines[$endIndex].Substring(0, $lines[$endIndex].Length - $lines[$endIndex].TrimStart().Length)
    $lines.Insert($endIndex, "$indent$registryLine")
    Write-TextFile $registryPath ($lines -join $newline)

    # Build.
    dotnet build "src/Modules/$Name/$Name.Api/$Name.Api.csproj"
    Assert-LastExitCode 'dotnet build'

    # Initial migration.
    if (-not $SkipMigration) {
        dotnet tool restore
        Assert-LastExitCode 'dotnet tool restore'

        $setConnection = [string]::IsNullOrEmpty($env:ConnectionStrings__Default)
        if ($setConnection) {
            $env:ConnectionStrings__Default = 'Host=localhost;Database=design'
        }
        try {
            dotnet ef migrations add Initial `
                --project "src/Modules/$Name/$Name.Infrastructure" `
                --startup-project src/Host `
                --context "${Name}DbContext" `
                --output-dir Migrations
            Assert-LastExitCode 'dotnet ef migrations add'
        }
        finally {
            if ($setConnection) {
                Remove-Item Env:\ConnectionStrings__Default -ErrorAction SilentlyContinue
            }
        }

        $migrations = Join-Path $moduleDir "$Name.Infrastructure/Migrations"
        foreach ($generated in Get-ChildItem -Path $migrations -Filter '*.cs') {
            $text = [System.IO.File]::ReadAllText($generated.FullName).Replace("`r`n", "`n")
            Write-TextFile $generated.FullName $text
        }
    }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "Module '$Name' created."
Write-Host "  Projects:  src/backend/src/Modules/$Name ($($layers -join ', '))"
Write-Host '  Solution:  projects added to Laundry.Backend.slnx'
Write-Host "  Host:      reference to $Name.Api added"
Write-Host "  Registry:  $registryLine"
if ($SkipMigration) {
    Write-Host '  Migration: skipped'
}
else {
    Write-Host "  Migration: Initial (schema '$lower')"
}
