[CmdletBinding()]
param(
    [string]$Version,
    [string]$CredentialPath = (Join-Path $env:APPDATA "Focus\Secrets\focus-ftps.clixml"),
    [ValidateSet("Patch", "Minor", "Major")]
    [string]$Increment = "Patch",
    [ValidateSet("Explicit", "Implicit")]
    [string]$FtpsMode = "Explicit",
    [string]$TlsHostCertificateFingerprint,
    [switch]$UpdateCredential,
    [switch]$SkipBuild,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue)
{
    $PSNativeCommandUseErrorActionPreference = $true
}

function Invoke-ReleaseAndUploadLauncher
{
    param(
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$CredentialPath,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch",
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit",
        [string]$TlsHostCertificateFingerprint,
        [switch]$UpdateCredential,
        [switch]$SkipBuild,
        [switch]$DryRun
    )

    $releaseAndUploadParameters = @{
        RemoteBaseUrl  = "sftp://192.168.100.7/var/www/html/Releases"
        CredentialPath = $CredentialPath
        Increment      = $Increment
        FtpsMode       = $FtpsMode
        UpdateCredential = $UpdateCredential
        SkipBuild      = $SkipBuild
        DryRun         = $DryRun
    }

    if (-not [string]::IsNullOrWhiteSpace($Version))
    {
        $releaseAndUploadParameters.Version = $Version
    }

    if (-not [string]::IsNullOrWhiteSpace($TlsHostCertificateFingerprint))
    {
        $releaseAndUploadParameters.TlsHostCertificateFingerprint = $TlsHostCertificateFingerprint
    }

    & (Join-Path $PSScriptRoot "ReleaseAndUpload.ps1") @releaseAndUploadParameters
    $releaseAndUploadExitCode = Get-Variable -Name LASTEXITCODE -ValueOnly -ErrorAction SilentlyContinue
    if (($null -ne $releaseAndUploadExitCode) -and ($releaseAndUploadExitCode -ne 0))
    {
        throw "ReleaseAndUpload.ps1 failed with exit code $releaseAndUploadExitCode."
    }
}

if ($MyInvocation.InvocationName -ne '.')
{
    Invoke-ReleaseAndUploadLauncher `
        -Version $Version `
        -CredentialPath $CredentialPath `
        -Increment $Increment `
        -FtpsMode $FtpsMode `
        -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint `
        -UpdateCredential:$UpdateCredential `
        -SkipBuild:$SkipBuild `
        -DryRun:$DryRun
}
