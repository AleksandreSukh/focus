[CmdletBinding()]
param(
    [string]$Version,
    [string]$RemoteUser,
    [Parameter(Mandatory = $true)]
    [string]$BundleId,
    [string]$IconPath = (Join-Path $PSScriptRoot "Packaging\mac\Focus.icns"),
    [Parameter(Mandatory = $true)]
    [string]$SignAppIdentity,
    [Parameter(Mandatory = $true)]
    [string]$SignInstallIdentity,
    [Parameter(Mandatory = $true)]
    [string]$NotaryProfile,
    [string]$Keychain,
    [string]$SignEntitlements,
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

function Invoke-ReleaseAndUploadMacLauncher
{
    param(
        [string]$Version,
        [string]$RemoteUser,
        [Parameter(Mandatory = $true)]
        [string]$BundleId,
        [Parameter(Mandatory = $true)]
        [string]$IconPath,
        [Parameter(Mandatory = $true)]
        [string]$SignAppIdentity,
        [Parameter(Mandatory = $true)]
        [string]$SignInstallIdentity,
        [Parameter(Mandatory = $true)]
        [string]$NotaryProfile,
        [string]$Keychain,
        [string]$SignEntitlements,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch",
        [switch]$SkipBuild,
        [switch]$DryRun
    )

    $releaseAndUploadParameters = @{
        RemoteBaseUrl       = "sftp://192.168.100.7/var/www/html/Releases"
        BundleId            = $BundleId
        IconPath            = $IconPath
        SignAppIdentity     = $SignAppIdentity
        SignInstallIdentity = $SignInstallIdentity
        NotaryProfile       = $NotaryProfile
        Increment           = $Increment
        SkipBuild           = $SkipBuild
        DryRun              = $DryRun
    }

    if (-not [string]::IsNullOrWhiteSpace($Version))
    {
        $releaseAndUploadParameters.Version = $Version
    }

    if (-not [string]::IsNullOrWhiteSpace($RemoteUser))
    {
        $releaseAndUploadParameters.RemoteUser = $RemoteUser
    }

    if (-not [string]::IsNullOrWhiteSpace($Keychain))
    {
        $releaseAndUploadParameters.Keychain = $Keychain
    }

    if (-not [string]::IsNullOrWhiteSpace($SignEntitlements))
    {
        $releaseAndUploadParameters.SignEntitlements = $SignEntitlements
    }

    & (Join-Path $PSScriptRoot "ReleaseAndUploadMac.ps1") @releaseAndUploadParameters
    $releaseAndUploadExitCode = Get-Variable -Name LASTEXITCODE -ValueOnly -ErrorAction SilentlyContinue
    if (($null -ne $releaseAndUploadExitCode) -and ($releaseAndUploadExitCode -ne 0))
    {
        throw "ReleaseAndUploadMac.ps1 failed with exit code $releaseAndUploadExitCode."
    }
}

if ($MyInvocation.InvocationName -ne '.')
{
    try
    {
        Invoke-ReleaseAndUploadMacLauncher `
            -Version $Version `
            -RemoteUser $RemoteUser `
            -BundleId $BundleId `
            -IconPath $IconPath `
            -SignAppIdentity $SignAppIdentity `
            -SignInstallIdentity $SignInstallIdentity `
            -NotaryProfile $NotaryProfile `
            -Keychain $Keychain `
            -SignEntitlements $SignEntitlements `
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
