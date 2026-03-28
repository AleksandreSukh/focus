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

function Resolve-OpenSshExecutablePath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    $commandInfo = Get-Command $CommandName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $commandInfo -or [string]::IsNullOrWhiteSpace($commandInfo.Source))
    {
        throw "$CommandName is required for Linux release uploads. Make sure OpenSSH client tools are available."
    }

    return $commandInfo.Source
}

function ConvertTo-PosixShellQuotedValue
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    return "'" + $Value.Replace("'", "'""'""'") + "'"
}

function New-SshConnectionTarget
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [string]$RemoteUser
    )

    $hostName = $RemoteEndpoint.Uri.Host
    if ($hostName.Contains(":") -and -not $hostName.StartsWith("["))
    {
        $hostName = "[$hostName]"
    }

    if ([string]::IsNullOrWhiteSpace($RemoteUser))
    {
        return $hostName
    }

    return "$RemoteUser@$hostName"
}

function Get-OpenSshPortArguments
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [switch]$ForScp
    )

    if ($RemoteEndpoint.Uri.IsDefaultPort -or $RemoteEndpoint.Uri.Port -le 0)
    {
        return @()
    }

    if ($ForScp)
    {
        return @("-P", $RemoteEndpoint.Uri.Port.ToString())
    }

    return @("-p", $RemoteEndpoint.Uri.Port.ToString())
}

function Invoke-SshCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [string]$RemoteUser,
        [Parameter(Mandatory = $true)]
        [string]$CommandText
    )

    $connectionTarget = New-SshConnectionTarget -RemoteEndpoint $RemoteEndpoint -RemoteUser $RemoteUser
    $output = & $ExecutablePath @(Get-OpenSshPortArguments -RemoteEndpoint $RemoteEndpoint) $connectionTarget $CommandText
    if ($LASTEXITCODE)
    {
        throw "ssh failed with exit code $LASTEXITCODE while running remote command."
    }

    return @($output)
}

function Get-RemoteReleaseFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$SshExecutablePath,
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [string]$RemoteUser
    )

    $quotedDirectory = ConvertTo-PosixShellQuotedValue -Value $RemoteEndpoint.RemoteDirectory
    $commandText = "mkdir -p -- $quotedDirectory && find $quotedDirectory -mindepth 1 -maxdepth 1 -type f -printf '%f\n' | LC_ALL=C sort -u"
    $remoteOutput = Invoke-SshCommand `
        -ExecutablePath $SshExecutablePath `
        -RemoteEndpoint $RemoteEndpoint `
        -RemoteUser $RemoteUser `
        -CommandText $commandText

    return @(
        $remoteOutput |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
}

function Invoke-RemoteReleaseChanges
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$SshExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$ScpExecutablePath,
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [string]$RemoteUser,
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$UploadFiles,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$DeleteFileNames
    )

    $connectionTarget = New-SshConnectionTarget -RemoteEndpoint $RemoteEndpoint -RemoteUser $RemoteUser
    $quotedDirectory = ConvertTo-PosixShellQuotedValue -Value $RemoteEndpoint.RemoteDirectory
    [void](Invoke-SshCommand `
        -ExecutablePath $SshExecutablePath `
        -RemoteEndpoint $RemoteEndpoint `
        -RemoteUser $RemoteUser `
        -CommandText "mkdir -p -- $quotedDirectory")

    $scpPortArguments = @(Get-OpenSshPortArguments -RemoteEndpoint $RemoteEndpoint -ForScp)

    foreach ($uploadFile in $UploadFiles)
    {
        $remoteFilePath = Join-FocusRemotePosixPath -Directory $RemoteEndpoint.RemoteDirectory -Child $uploadFile.Name
        $remoteTarget = '{0}:{1}' -f $connectionTarget, (ConvertTo-PosixShellQuotedValue -Value $remoteFilePath)
        & $ScpExecutablePath @scpPortArguments $uploadFile.FullName $remoteTarget
        if ($LASTEXITCODE)
        {
            throw "scp failed with exit code $LASTEXITCODE while uploading '$($uploadFile.Name)'."
        }
    }

    foreach ($deleteFileName in $DeleteFileNames)
    {
        $remoteFilePath = Join-FocusRemotePosixPath -Directory $RemoteEndpoint.RemoteDirectory -Child $deleteFileName
        $quotedRemoteFilePath = ConvertTo-PosixShellQuotedValue -Value $remoteFilePath
        [void](Invoke-SshCommand `
            -ExecutablePath $SshExecutablePath `
            -RemoteEndpoint $RemoteEndpoint `
            -RemoteUser $RemoteUser `
            -CommandText "rm -f -- $quotedRemoteFilePath")
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
        $remoteFileNames = Get-RemoteReleaseFileNames `
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

        Invoke-RemoteReleaseChanges `
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
