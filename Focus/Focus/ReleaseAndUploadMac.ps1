[CmdletBinding()]
param(
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$RemoteBaseUrl,
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

. (Join-Path $PSScriptRoot "ReleaseAndUpload.Shared.ps1")

function Get-MacPublishDirectoryPath
{
    return Join-Path $PSScriptRoot "publish-mac"
}

function Assert-MacReleaseHost
{
    if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX))
    {
        throw "ReleaseAndUploadMac.ps1 must be run on macOS."
    }
}

function Resolve-MacIconPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$IconPath
    )

    if ([string]::IsNullOrWhiteSpace($IconPath))
    {
        throw "IconPath cannot be empty."
    }

    if (-not (Test-Path -LiteralPath $IconPath -PathType Leaf))
    {
        throw "macOS release icon '$IconPath' was not found."
    }

    return (Resolve-Path -LiteralPath $IconPath).ProviderPath
}

function Assert-MacReleaseParameters
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundleId,
        [Parameter(Mandatory = $true)]
        [string]$SignAppIdentity,
        [Parameter(Mandatory = $true)]
        [string]$SignInstallIdentity,
        [Parameter(Mandatory = $true)]
        [string]$NotaryProfile
    )

    foreach ($requiredValue in @{
        BundleId            = $BundleId
        SignAppIdentity     = $SignAppIdentity
        SignInstallIdentity = $SignInstallIdentity
        NotaryProfile       = $NotaryProfile
    }.GetEnumerator())
    {
        if ([string]::IsNullOrWhiteSpace($requiredValue.Value))
        {
            throw "$($requiredValue.Key) cannot be empty."
        }
    }
}

function Invoke-MacBuildAndPublish
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$DotnetExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$VpkExecutablePath,
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
        [string]$SignEntitlements
    )

    if ([string]::IsNullOrWhiteSpace($Version))
    {
        throw "Version cannot be empty."
    }

    Assert-MacReleaseParameters `
        -BundleId $BundleId `
        -SignAppIdentity $SignAppIdentity `
        -SignInstallIdentity $SignInstallIdentity `
        -NotaryProfile $NotaryProfile
    $resolvedIconPath = Resolve-MacIconPath -IconPath $IconPath

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        Write-Host "Publishing Focus $Version for osx-arm64..."

        & $dotnetExecutablePath publish Systems.Sanity.Focus.csproj -c Release --self-contained -r osx-arm64 -o .\publish-mac
        if ($LASTEXITCODE)
        {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }

        $vpkPackArguments = @(
            "pack"
            "-u"
            "Focus"
            "-v"
            $Version
            "-p"
            ".\publish-mac"
            "-r"
            "osx-arm64"
            "-e"
            "Systems.Sanity.Focus"
            "--packTitle"
            "Focus"
            "--icon"
            $resolvedIconPath
            "--bundleId"
            $BundleId.Trim()
            "--signAppIdentity"
            $SignAppIdentity.Trim()
            "--signInstallIdentity"
            $SignInstallIdentity.Trim()
            "--notaryProfile"
            $NotaryProfile.Trim()
        )

        if (-not [string]::IsNullOrWhiteSpace($Keychain))
        {
            $vpkPackArguments += @("--keychain", $Keychain.Trim())
        }

        if (-not [string]::IsNullOrWhiteSpace($SignEntitlements))
        {
            $vpkPackArguments += @("--signEntitlements", $SignEntitlements.Trim())
        }

        & $vpkExecutablePath @vpkPackArguments
        if ($LASTEXITCODE)
        {
            throw "vpk pack failed with exit code $LASTEXITCODE."
        }
    }
    finally
    {
        Pop-Location
    }
}

function Invoke-ReleaseAndUploadMac
{
    param(
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$RemoteBaseUrl,
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

    Assert-MacReleaseHost
    Assert-MacReleaseParameters `
        -BundleId $BundleId `
        -SignAppIdentity $SignAppIdentity `
        -SignInstallIdentity $SignInstallIdentity `
        -NotaryProfile $NotaryProfile
    [void](Resolve-MacIconPath -IconPath $IconPath)

    $releaseVersion = Resolve-FocusReleaseVersionString -Platform Mac -Version $Version -Increment $Increment -SkipBuild:$SkipBuild
    Write-Host "Release version: $releaseVersion"

    $remoteEndpoint = Resolve-FocusRemoteEndpoint -BaseUrl $RemoteBaseUrl -AllowedSchemes @("sftp")
    $dotnetExecutablePath = Resolve-RequiredExecutablePath `
        -CommandName "dotnet" `
        -FailureMessage "dotnet is required for macOS release builds. Install the .NET SDK."
    $vpkExecutablePath = Resolve-RequiredExecutablePath `
        -CommandName "vpk" `
        -FailureMessage "vpk is required for macOS release builds. Install the Velopack CLI."
    $sshExecutablePath = Resolve-OpenSshExecutablePath -CommandName "ssh"
    $scpExecutablePath = Resolve-OpenSshExecutablePath -CommandName "scp"

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        if (-not $SkipBuild)
        {
            Invoke-MacBuildAndPublish `
                -Version $releaseVersion `
                -DotnetExecutablePath $dotnetExecutablePath `
                -VpkExecutablePath $vpkExecutablePath `
                -BundleId $BundleId `
                -IconPath $IconPath `
                -SignAppIdentity $SignAppIdentity `
                -SignInstallIdentity $SignInstallIdentity `
                -NotaryProfile $NotaryProfile `
                -Keychain $Keychain `
                -SignEntitlements $SignEntitlements
        }

        $localReleaseFiles = Get-FocusLocalReleaseFiles -Platform Mac -ReleaseDirectoryPath (Get-FocusReleasesDirectoryPath)

        Write-Host "Listing remote files in '$($remoteEndpoint.RemoteDirectory)'..."
        $remoteFileNames = Get-SftpRemoteReleaseFileNames `
            -SshExecutablePath $sshExecutablePath `
            -RemoteEndpoint $remoteEndpoint `
            -RemoteUser $RemoteUser

        $syncPlan = Get-FocusReleaseSyncPlan -Platform Mac -LocalReleaseFiles $localReleaseFiles -RemoteFileNames $remoteFileNames
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
        Invoke-ReleaseAndUploadMac `
            -Version $Version `
            -RemoteBaseUrl $RemoteBaseUrl `
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
