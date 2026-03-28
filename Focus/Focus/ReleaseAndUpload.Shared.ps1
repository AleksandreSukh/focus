$script:FocusReleaseScriptRoot = $PSScriptRoot

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

function Get-FocusPlatformManagedExactFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return @("RELEASES", "releases.win.json", "assets.win.json", "Focus-win-Setup.exe", "Focus-win-Portable.zip") }
        "Linux" { return @("RELEASES-linux", "releases.linux.json", "assets.linux.json", "Focus.AppImage") }
    }
}

function Get-FocusPlatformMetadataFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return @("assets.win.json", "releases.win.json", "RELEASES") }
        "Linux" { return @("assets.linux.json", "releases.linux.json", "RELEASES-linux") }
    }
}

function Get-FocusPlatformRequiredExactFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
        [string]$Platform
    )

    return @(Get-FocusPlatformManagedExactFileNames -Platform $Platform)
}

function Get-FocusPlatformManagedRegexPatterns
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return @('^Focus-\d+\.\d+\.\d+-(full|delta)\.nupkg$') }
        "Linux" { return @('^Focus-\d+\.\d+\.\d+-linux-(full|delta)\.nupkg$') }
    }
}

function Get-FocusPlatformFullPackageRegex
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
        [string]$Platform
    )

    switch ($Platform)
    {
        "Windows" { return '^Focus-\d+\.\d+\.\d+-full\.nupkg$' }
        "Linux" { return '^Focus-\d+\.\d+\.\d+-linux-full\.nupkg$' }
    }
}

function Test-FocusPlatformManagedFileName
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $managedExactFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($managedExactFileName in (Get-FocusPlatformManagedExactFileNames -Platform $Platform))
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
        [ValidateSet("Windows", "Linux")]
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
    }

    return $null
}

function Get-FocusLatestPlatformArtifactReleaseVersion
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
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
                @(
                    Get-FocusVersionFromManagedFileName -Platform Windows -FileName $_.Name
                    Get-FocusVersionFromManagedFileName -Platform Linux -FileName $_.Name
                )
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
        [ValidateSet("Windows", "Linux")]
        [string]$Platform,
        [string]$Version,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment,
        [switch]$SkipBuild
    )

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

function Get-FocusLocalReleaseFiles
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
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
            Where-Object { Test-FocusPlatformManagedFileName -Platform $Platform -FileName $_.Name } |
            Sort-Object Name
    )

    if (-not $managedReleaseFiles)
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain any $($Platform.ToLowerInvariant()) release files."
    }

    foreach ($requiredExactName in (Get-FocusPlatformRequiredExactFileNames -Platform $Platform))
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
        [ValidateSet("Windows", "Linux")]
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
        [ValidateSet("Windows", "Linux")]
        [string]$Platform,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$RemoteFileNames
    )

    return @(
        $RemoteFileNames |
            Where-Object { Test-FocusPlatformManagedFileName -Platform $Platform -FileName $_ } |
            Sort-Object -Unique
    )
}

function Get-FocusReleaseSyncPlan
{
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Windows", "Linux")]
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

    $filesToDelete = Get-FocusManagedRemoteFileNames -Platform $Platform -RemoteFileNames $RemoteFileNames |
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
