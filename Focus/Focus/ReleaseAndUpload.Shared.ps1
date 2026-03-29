$script:FocusReleaseScriptRoot = $PSScriptRoot

function Get-FocusSupportedPlatforms
{
    return @("Windows", "Linux", "Mac")
}

function Get-FocusDefaultBuildVersion
{
    return [Version]"1.0.27"
}

function Get-FocusReleasesDirectoryPath
{
    return Join-Path $script:FocusReleaseScriptRoot "Releases"
}

function Join-FocusRemotePosixPath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory,
        [Parameter(Mandatory = $true)]
        [string]$Child
    )

    if ($Directory -eq "/")
    {
        return "/$Child"
    }

    return "{0}/{1}" -f $Directory.TrimEnd('/'), $Child
}

function ConvertTo-FocusReleaseVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    try
    {
        return [Version]$Value
    }
    catch
    {
        throw "Version '$Value' is not valid. Expected a value like 1.0.28."
    }
}

function Test-FocusAutoVersionRequested
{
    param(
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Version))
    {
        return $false
    }

    return $Version.Trim() -ieq "Auto"
}

function Get-FocusPlatformChannelName
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return "win" }
        "Linux" { return "linux" }
        "Mac" { return "osx" }
    }
}

function Get-FocusPlatformMetadataFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform
    )

    $channel = Get-FocusPlatformChannelName -Platform $Platform
    $legacyReleasesFileName = if ($Platform -eq "Windows") { "RELEASES" } else { "RELEASES-$channel" }

    return @(
        "assets.$channel.json"
        "releases.$channel.json"
        $legacyReleasesFileName
    )
}

function Get-FocusPlatformAssetsManifestPath
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    return Join-Path $ReleaseDirectoryPath ("assets.{0}.json" -f (Get-FocusPlatformChannelName -Platform $Platform))
}

function Get-FocusPlatformAssetsManifestEntries
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    $assetsManifestPath = Get-FocusPlatformAssetsManifestPath -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath
    if (-not (Test-Path -LiteralPath $assetsManifestPath -PathType Leaf))
    {
        return @()
    }

    try
    {
        $manifest = Get-Content -LiteralPath $assetsManifestPath -Raw | ConvertFrom-Json
    }
    catch
    {
        throw "Assets manifest '$assetsManifestPath' is not valid JSON."
    }

    return @($manifest)
}

function Get-FocusPlatformAssetRelativeFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    $assetsManifestPath = Get-FocusPlatformAssetsManifestPath -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath
    $assetEntries = @(Get-FocusPlatformAssetsManifestEntries -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath)
    if ($assetEntries.Count -eq 0)
    {
        return @()
    }

    $relativeFileNames = @(
        $assetEntries |
            ForEach-Object { $_.RelativeFileName } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )

    if ($relativeFileNames.Count -eq 0)
    {
        throw "Assets manifest '$assetsManifestPath' does not contain any RelativeFileName entries."
    }

    return $relativeFileNames
}

function Get-FocusPlatformManagedExactFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    $managedExactFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

    foreach ($fileName in (Get-FocusPlatformMetadataFileNames -Platform $Platform))
    {
        [void]$managedExactFileNames.Add($fileName)
    }

    foreach ($fileName in (Get-FocusPlatformAssetRelativeFileNames -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath))
    {
        [void]$managedExactFileNames.Add($fileName)
    }

    return @($managedExactFileNames | Sort-Object)
}

function Get-FocusPlatformRequiredExactFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    return @(Get-FocusPlatformManagedExactFileNames -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath)
}

function Get-FocusPlatformManagedRegexPatterns
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return @('^Focus-\d+\.\d+\.\d+-(full|delta)\.nupkg$') }
        "Linux" { return @('^Focus-\d+\.\d+\.\d+-linux-(full|delta)\.nupkg$') }
        "Mac" { return @('^Focus-\d+\.\d+\.\d+-osx-(full|delta)\.nupkg$') }
    }
}

function Get-FocusPlatformFullPackageRegex
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return '^Focus-\d+\.\d+\.\d+-full\.nupkg$' }
        "Linux" { return '^Focus-\d+\.\d+\.\d+-linux-full\.nupkg$' }
        "Mac" { return '^Focus-\d+\.\d+\.\d+-osx-full\.nupkg$' }
    }
}

function Test-FocusPlatformManagedFileName
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$FileName,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    $managedExactFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($managedExactFileName in (Get-FocusPlatformManagedExactFileNames -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath))
    {
        [void]$managedExactFileNames.Add($managedExactFileName)
    }

    if ($managedExactFileNames.Contains($FileName))
    {
        return $true
    }

    foreach ($pattern in (Get-FocusPlatformManagedRegexPatterns -Platform $Platform))
    {
        if ($FileName -match $pattern)
        {
            return $true
        }
    }

    return $false
}

function Get-FocusVersionFromManagedFileName
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    switch ($Platform)
    {
        "Windows"
        {
            if ($FileName -match '^Focus-(?<Version>\d+\.\d+\.\d+)-full\.nupkg$')
            {
                return [Version]$Matches.Version
            }
            break
        }
        "Linux"
        {
            if ($FileName -match '^Focus-(?<Version>\d+\.\d+\.\d+)-linux-full\.nupkg$')
            {
                return [Version]$Matches.Version
            }
            break
        }
        "Mac"
        {
            if ($FileName -match '^Focus-(?<Version>\d+\.\d+\.\d+)-osx-full\.nupkg$')
            {
                return [Version]$Matches.Version
            }
            break
        }
    }

    return $null
}

function Get-FocusLatestPlatformArtifactReleaseVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    if (-not (Test-Path -LiteralPath $ReleaseDirectoryPath -PathType Container))
    {
        return $null
    }

    $versions = @(
        Get-ChildItem -LiteralPath $ReleaseDirectoryPath -File |
            ForEach-Object { Get-FocusVersionFromManagedFileName -Platform $Platform -FileName $_.Name } |
            Where-Object { $null -ne $_ } |
            Sort-Object -Descending
    )

    if ($versions.Count -eq 0)
    {
        return $null
    }

    return $versions[0]
}

function Get-FocusLatestManagedReleaseVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    if (-not (Test-Path -LiteralPath $ReleaseDirectoryPath -PathType Container))
    {
        return $null
    }

    $versions = @(
        Get-ChildItem -LiteralPath $ReleaseDirectoryPath -File |
            ForEach-Object {
                foreach ($platform in (Get-FocusSupportedPlatforms))
                {
                    Get-FocusVersionFromManagedFileName -Platform $platform -FileName $_.Name
                }
            } |
            Where-Object { $null -ne $_ } |
            Sort-Object -Descending
    )

    if ($versions.Count -eq 0)
    {
        return $null
    }

    return $versions[0]
}

function Get-FocusLatestManagedReleaseVersionFromFileNames
{
    param(
        [AllowEmptyCollection()]
        [string[]]$FileNames
    )

    if ($null -eq $FileNames -or $FileNames.Count -eq 0)
    {
        return $null
    }

    $versions = @(
        $FileNames |
            ForEach-Object {
                foreach ($platform in (Get-FocusSupportedPlatforms))
                {
                    Get-FocusVersionFromManagedFileName -Platform $platform -FileName $_
                }
            } |
            Where-Object { $null -ne $_ } |
            Sort-Object -Descending
    )

    if ($versions.Count -eq 0)
    {
        return $null
    }

    return $versions[0]
}

function Get-FocusIncrementedReleaseVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [Version]$CurrentVersion,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch"
    )

    $currentPatch = if ($CurrentVersion.Build -lt 0) { 0 } else { $CurrentVersion.Build }

    switch ($Increment)
    {
        "Major" { return [Version]::new($CurrentVersion.Major + 1, 0, 0) }
        "Minor" { return [Version]::new($CurrentVersion.Major, $CurrentVersion.Minor + 1, 0) }
        default { return [Version]::new($CurrentVersion.Major, $CurrentVersion.Minor, $currentPatch + 1) }
    }
}

function Resolve-FocusReleaseVersionString
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [string]$Version,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch",
        [switch]$SkipBuild,
        [AllowEmptyCollection()]
        [string[]]$RemoteFileNames,
        [string]$VersionSourceDescription = "remote release directory"
    )

    if (Test-FocusAutoVersionRequested -Version $Version)
    {
        if ($SkipBuild)
        {
            throw "Cannot combine -Version Auto with -SkipBuild because -SkipBuild uploads existing artifacts without producing a new version."
        }

        $currentVersion = Get-FocusLatestManagedReleaseVersionFromFileNames -FileNames $RemoteFileNames
        if ($null -eq $currentVersion)
        {
            throw "Cannot resolve -Version Auto because $VersionSourceDescription does not contain any managed release packages."
        }

        return (Get-FocusIncrementedReleaseVersion -CurrentVersion $currentVersion -Increment Patch).ToString(3)
    }

    if (-not [string]::IsNullOrWhiteSpace($Version))
    {
        return (ConvertTo-FocusReleaseVersion -Value $Version).ToString(3)
    }

    $releaseDirectoryPath = Get-FocusReleasesDirectoryPath
    $latestPlatformArtifactVersion = Get-FocusLatestPlatformArtifactReleaseVersion -Platform $Platform -ReleaseDirectoryPath $releaseDirectoryPath
    if ($SkipBuild)
    {
        if ($null -eq $latestPlatformArtifactVersion)
        {
            throw "Cannot determine which $($Platform.ToLowerInvariant()) release to upload with -SkipBuild because no local release artifacts were found."
        }

        return $latestPlatformArtifactVersion.ToString(3)
    }

    $currentVersion = Get-FocusLatestManagedReleaseVersion -ReleaseDirectoryPath $releaseDirectoryPath
    $defaultBuildVersion = Get-FocusDefaultBuildVersion
    if ($null -eq $currentVersion -or $defaultBuildVersion -gt $currentVersion)
    {
        $currentVersion = $defaultBuildVersion
    }

    if ($null -eq $currentVersion)
    {
        $currentVersion = [Version]"1.0.0"
    }

    return (Get-FocusIncrementedReleaseVersion -CurrentVersion $currentVersion -Increment $Increment).ToString(3)
}

function Resolve-FocusRemoteEndpoint
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,
        [string[]]$AllowedSchemes = @("ftp", "ftps", "ftpes", "sftp")
    )

    $normalizedBaseUrl = $BaseUrl.Trim()
    if ([string]::IsNullOrWhiteSpace($normalizedBaseUrl))
    {
        throw "Remote base URL cannot be empty."
    }

    if (-not [Uri]::TryCreate($normalizedBaseUrl, [UriKind]::Absolute, [ref]$null))
    {
        throw "Remote base URL '$normalizedBaseUrl' is not a valid absolute URL."
    }

    $uri = [Uri]$normalizedBaseUrl
    if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo))
    {
        throw "Do not include credentials in '$normalizedBaseUrl'. Use the script parameters or SSH configuration instead."
    }

    $scheme = $uri.Scheme.ToLowerInvariant()
    if ($AllowedSchemes -notcontains $scheme)
    {
        throw "Remote base URL must use one of: $($AllowedSchemes -join ', ')."
    }

    $remoteDirectory = [Uri]::UnescapeDataString($uri.AbsolutePath)
    if ([string]::IsNullOrWhiteSpace($remoteDirectory))
    {
        $remoteDirectory = "/"
    }
    else
    {
        $remoteDirectory = $remoteDirectory.TrimEnd('/')
        if ([string]::IsNullOrWhiteSpace($remoteDirectory))
        {
            $remoteDirectory = "/"
        }
        elseif (-not $remoteDirectory.StartsWith("/"))
        {
            $remoteDirectory = "/$remoteDirectory"
        }
    }

    return [PSCustomObject]@{
        BaseUrl         = $normalizedBaseUrl
        Uri             = $uri
        Scheme          = $scheme
        RemoteDirectory = $remoteDirectory
    }
}

function Resolve-RequiredExecutablePath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    $commandInfo = Get-Command $CommandName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $commandInfo -or [string]::IsNullOrWhiteSpace($commandInfo.Source))
    {
        throw $FailureMessage
    }

    return $commandInfo.Source
}

function Resolve-OpenSshExecutablePath
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    return Resolve-RequiredExecutablePath `
        -CommandName $CommandName `
        -FailureMessage "$CommandName is required for SFTP release uploads. Make sure OpenSSH client tools are available."
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

function New-SftpConnectionTarget
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

function Invoke-SftpSshCommand
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

    $connectionTarget = New-SftpConnectionTarget -RemoteEndpoint $RemoteEndpoint -RemoteUser $RemoteUser
    $output = & $ExecutablePath @(Get-OpenSshPortArguments -RemoteEndpoint $RemoteEndpoint) $connectionTarget $CommandText
    if ($LASTEXITCODE)
    {
        throw "ssh failed with exit code $LASTEXITCODE while running remote command."
    }

    return @($output)
}

function Get-SftpRemoteReleaseFileNames
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
    $remoteOutput = Invoke-SftpSshCommand `
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

function Invoke-SftpRemoteReleaseChanges
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

    $connectionTarget = New-SftpConnectionTarget -RemoteEndpoint $RemoteEndpoint -RemoteUser $RemoteUser
    $quotedDirectory = ConvertTo-PosixShellQuotedValue -Value $RemoteEndpoint.RemoteDirectory
    [void](Invoke-SftpSshCommand `
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
        [void](Invoke-SftpSshCommand `
            -ExecutablePath $SshExecutablePath `
            -RemoteEndpoint $RemoteEndpoint `
            -RemoteUser $RemoteUser `
            -CommandText "rm -f -- $quotedRemoteFilePath")
    }
}

function Get-FocusLocalReleaseFiles
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    if (-not (Test-Path -LiteralPath $ReleaseDirectoryPath -PathType Container))
    {
        throw "Releases directory '$ReleaseDirectoryPath' was not found."
    }

    $managedReleaseFiles = @(
        Get-ChildItem -LiteralPath $ReleaseDirectoryPath -File |
            Where-Object {
                Test-FocusPlatformManagedFileName `
                    -Platform $Platform `
                    -FileName $_.Name `
                    -ReleaseDirectoryPath $ReleaseDirectoryPath
            } |
            Sort-Object Name
    )

    if (-not $managedReleaseFiles)
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain any $($Platform.ToLowerInvariant()) release files."
    }

    foreach ($requiredExactName in (Get-FocusPlatformRequiredExactFileNames -Platform $Platform -ReleaseDirectoryPath $ReleaseDirectoryPath))
    {
        if (-not ($managedReleaseFiles.Name -contains $requiredExactName))
        {
            throw "Releases directory '$ReleaseDirectoryPath' is missing required $($Platform.ToLowerInvariant()) artifact '$requiredExactName'."
        }
    }

    $requiredFullPackageRegex = Get-FocusPlatformFullPackageRegex -Platform $Platform
    if (-not ($managedReleaseFiles.Name | Where-Object { $_ -match $requiredFullPackageRegex }))
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain a $($Platform.ToLowerInvariant()) full .nupkg package."
    }

    return $managedReleaseFiles
}

function Get-FocusUploadOrderedReleaseFiles
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$LocalReleaseFiles
    )

    $metadataOrder = @(Get-FocusPlatformMetadataFileNames -Platform $Platform)
    $metadataLookup = [Collections.Generic.Dictionary[string, System.IO.FileInfo]]::new([StringComparer]::Ordinal)
    $payloadFiles = [Collections.Generic.List[System.IO.FileInfo]]::new()

    foreach ($localReleaseFile in $LocalReleaseFiles)
    {
        if ($metadataOrder -contains $localReleaseFile.Name)
        {
            $metadataLookup[$localReleaseFile.Name] = $localReleaseFile
        }
        else
        {
            $payloadFiles.Add($localReleaseFile)
        }
    }

    $orderedMetadataFiles = [Collections.Generic.List[System.IO.FileInfo]]::new()
    foreach ($metadataFileName in $metadataOrder)
    {
        if ($metadataLookup.ContainsKey($metadataFileName))
        {
            $orderedMetadataFiles.Add($metadataLookup[$metadataFileName])
        }
    }

    return @(
        $payloadFiles | Sort-Object Name
        $orderedMetadataFiles
    )
}

function Get-FocusManagedRemoteFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$RemoteFileNames
    )

    return @(
        $RemoteFileNames |
            Where-Object {
                Test-FocusPlatformManagedFileName `
                    -Platform $Platform `
                    -FileName $_ `
                    -ReleaseDirectoryPath $ReleaseDirectoryPath
            } |
            Sort-Object -Unique
    )
}

function Get-FocusReleaseSyncPlan
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux", "Mac")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$LocalReleaseFiles,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$RemoteFileNames
    )

    $localFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($localReleaseFile in $LocalReleaseFiles)
    {
        [void]$localFileNames.Add($localReleaseFile.Name)
    }

    $releaseDirectoryPath = $LocalReleaseFiles[0].DirectoryName
    $filesToDelete = Get-FocusManagedRemoteFileNames `
        -Platform $Platform `
        -ReleaseDirectoryPath $releaseDirectoryPath `
        -RemoteFileNames $RemoteFileNames |
        Where-Object { -not $localFileNames.Contains($_) } |
        Sort-Object

    return [PSCustomObject]@{
        UploadFiles     = @(Get-FocusUploadOrderedReleaseFiles -Platform $Platform -LocalReleaseFiles $LocalReleaseFiles)
        DeleteFileNames = @($filesToDelete)
    }
}

function Write-FocusReleaseSyncPlan
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$SyncPlan
    )

    Write-Host ""
    Write-Host "Upload plan:"
    foreach ($uploadFile in $SyncPlan.UploadFiles)
    {
        Write-Host "  upload $($uploadFile.Name)"
    }

    Write-Host ""
    Write-Host "Delete plan:"
    if ($SyncPlan.DeleteFileNames.Count -eq 0)
    {
        Write-Host "  nothing to delete"
        return
    }

    foreach ($deleteFileName in $SyncPlan.DeleteFileNames)
    {
        Write-Host "  delete $deleteFileName"
    }
}
