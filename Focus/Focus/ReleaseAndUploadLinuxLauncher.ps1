[CmdletBinding()]
param(
    [string]$Version,
    [string]$RemoteUser,
    [ValidateSet("Patch", "Minor", "Major")]
    [string]$Increment = "Patch",
    [switch]$SkipBuild,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue)
{
    $PSNativeCommandUseErrorActionPreference = $true
}

function Invoke-ReleaseAndUploadLinuxLauncher
{
    param(
        [string]$Version,
        [string]$RemoteUser,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch",
        [switch]$SkipBuild,
        [switch]$DryRun
    )

    $releaseAndUploadParameters = @{
        RemoteBaseUrl = "sftp://192.168.100.7/var/www/html/Releases"
        Increment     = $Increment
        SkipBuild     = $SkipBuild
        DryRun        = $DryRun
    }

    if (-not [string]::IsNullOrWhiteSpace($Version))
    {
        $releaseAndUploadParameters.Version = $Version
    }

    if (-not [string]::IsNullOrWhiteSpace($RemoteUser))
    {
        $releaseAndUploadParameters.RemoteUser = $RemoteUser
    }

    & (Join-Path $PSScriptRoot "ReleaseAndUploadLinux.ps1") @releaseAndUploadParameters
    $releaseAndUploadExitCode = Get-Variable -Name LASTEXITCODE -ValueOnly -ErrorAction SilentlyContinue
    if (($null -ne $releaseAndUploadExitCode) -and ($releaseAndUploadExitCode -ne 0))
    {
        throw "ReleaseAndUploadLinux.ps1 failed with exit code $releaseAndUploadExitCode."
    }
}

if ($MyInvocation.InvocationName -ne '.')
{
    try
    {
        Invoke-ReleaseAndUploadLinuxLauncher `
            -Version $Version `
            -RemoteUser $RemoteUser `
            -Increment $Increment `
            -SkipBuild:$SkipBuild `
            -DryRun:$DryRun
    }
    catch
    {
        Write-Error $_
        exit 1
    }
}
