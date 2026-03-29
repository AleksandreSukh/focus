[CmdletBinding()]
param(
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$RemoteBaseUrl,
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

. (Join-Path $PSScriptRoot "ReleaseAndUpload.Shared.ps1")

function Get-LinuxPublishDirectoryPath
{
    return Join-Path $PSScriptRoot "publish-linux"
}

function Invoke-LinuxBuildAndPublish
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Version))
    {
        throw "Version cannot be empty."
    }

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        Write-Host "Publishing Focus $Version for linux-x64..."

        & dotnet publish Systems.Sanity.Focus.csproj -c Release --self-contained -r linux-x64 -o .\publish-linux
        if ($LASTEXITCODE)
        {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }

        & vpk "[linux]" pack -u Focus -v $Version -p .\publish-linux -r linux-x64 -e Systems.Sanity.Focus --packTitle Focus
        if ($LASTEXITCODE)
        {
            throw "vpk [linux] pack failed with exit code $LASTEXITCODE."
        }
    }
    finally
    {
        Pop-Location
    }
}

function Invoke-ReleaseAndUploadLinux
{
    param(
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$RemoteBaseUrl,
        [string]$RemoteUser,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch",
        [switch]$SkipBuild,
        [switch]$DryRun
    )

    $releaseVersion = Resolve-FocusReleaseVersionString -Platform Linux -Version $Version -Increment $Increment -SkipBuild:$SkipBuild
    Write-Host "Release version: $releaseVersion"

    $remoteEndpoint = Resolve-FocusRemoteEndpoint -BaseUrl $RemoteBaseUrl -AllowedSchemes @("sftp")
    $sshExecutablePath = Resolve-OpenSshExecutablePath -CommandName "ssh"
    $scpExecutablePath = Resolve-OpenSshExecutablePath -CommandName "scp"

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        if (-not $SkipBuild)
        {
            Invoke-LinuxBuildAndPublish -Version $releaseVersion
        }

        $localReleaseFiles = Get-FocusLocalReleaseFiles -Platform Linux -ReleaseDirectoryPath (Get-FocusReleasesDirectoryPath)

        Write-Host "Listing remote files in '$($remoteEndpoint.RemoteDirectory)'..."
        $remoteFileNames = Get-SftpRemoteReleaseFileNames `
            -SshExecutablePath $sshExecutablePath `
            -RemoteEndpoint $remoteEndpoint `
            -RemoteUser $RemoteUser

        $syncPlan = Get-FocusReleaseSyncPlan -Platform Linux -LocalReleaseFiles $localReleaseFiles -RemoteFileNames $remoteFileNames
        Write-FocusReleaseSyncPlan -SyncPlan $syncPlan

        if ($DryRun)
        {
            Write-Host ""
            Write-Host "Dry run completed. No remote changes were made."
            return
        }

        foreach ($uploadFile in $syncPlan.UploadFiles)
        {
            Write-Host "Uploading $($uploadFile.Name)..."
        }

        foreach ($deleteFileName in $syncPlan.DeleteFileNames)
        {
            Write-Host "Deleting remote file $deleteFileName..."
        }

        Invoke-SftpRemoteReleaseChanges `
            -SshExecutablePath $sshExecutablePath `
            -ScpExecutablePath $scpExecutablePath `
            -RemoteEndpoint $remoteEndpoint `
            -RemoteUser $RemoteUser `
            -UploadFiles $syncPlan.UploadFiles `
            -DeleteFileNames $syncPlan.DeleteFileNames

        Write-Host ""
        Write-Host "Release mirror completed successfully."
    }
    finally
    {
        Pop-Location
    }
}

if ($MyInvocation.InvocationName -ne '.')
{
    try
    {
        Invoke-ReleaseAndUploadLinux `
            -Version $Version `
            -RemoteBaseUrl $RemoteBaseUrl `
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
