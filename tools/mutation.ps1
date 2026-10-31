#Requires -Version 7.0
<#
.SYNOPSIS
    Tests de mutation du domaine avec Stryker.NET.

.DESCRIPTION
    Stryker.NET ne sait pas encore piloter Microsoft Testing Platform : le script passe le projet
    de tests en mode VSTest le temps de l'analyse (variable KYFREV_MUTATION), puis restaure le
    mode ordinaire. Le rapport HTML est écrit dans tests/KyfRev.Domain.Tests/StrykerOutput.
    Le score minimal exigé est de 80 % (stryker-config.json).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$racine = Split-Path -Parent $PSScriptRoot
$tests = Join-Path $racine 'tests\KyfRev.Domain.Tests'

$env:KYFREV_MUTATION = '1'
try {
    dotnet restore (Join-Path $tests 'KyfRev.Domain.Tests.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Restauration en mode mutation impossible.' }
    Push-Location $tests
    try {
        dotnet stryker
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item Env:KYFREV_MUTATION
    dotnet restore (Join-Path $tests 'KyfRev.Domain.Tests.csproj') | Out-Null
}
exit $code
