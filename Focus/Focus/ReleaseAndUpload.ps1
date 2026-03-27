[CmdletBinding()]
param(
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [Alias("FtpsBaseUrl")]
    [string]$RemoteBaseUrl,
    [string]$CredentialPath = (Join-Path $env:APPDATA "Focus\Secrets\focus-ftps.clixml"),
    [ValidateSet("Patch", "Minor", "Major")]
    [string]$Increment = "Patch",
    [ValidateSet("Explicit", "Implicit")]
    [string]$FtpsMode = "Explicit",
    [string]$SshHostKeyFingerprint,
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

function Get-DefaultBuildVersion
{
    return [Version]"1.0.27"
}

function Get-ReleasesDirectoryPath
{
    return Join-Path $PSScriptRoot "Releases"
}

function ConvertTo-ReleaseVersion
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

function Get-LatestArtifactReleaseVersion
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
        Get-ChildItem -LiteralPath $ReleaseDirectoryPath -Filter "Focus-*-full.nupkg" -File |
            ForEach-Object {
                if ($_.BaseName -match '^Focus-(?<Version>\d+\.\d+\.\d+)-full$')
                {
                    [Version]$Matches.Version
                }
            } |
            Sort-Object -Descending
    )

    if ($versions.Count -eq 0)
    {
        return $null
    }

    return $versions[0]
}

function Get-IncrementedReleaseVersion
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

function Resolve-ReleaseVersionString
{
    param(
        [string]$Version,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment,
        [switch]$SkipBuild
    )

    if (-not [string]::IsNullOrWhiteSpace($Version))
    {
        return (ConvertTo-ReleaseVersion -Value $Version).ToString(3)
    }

    $latestArtifactVersion = Get-LatestArtifactReleaseVersion -ReleaseDirectoryPath (Get-ReleasesDirectoryPath)
    if ($SkipBuild)
    {
        if ($null -eq $latestArtifactVersion)
        {
            throw "Cannot determine which release to upload with -SkipBuild because no local release artifacts were found."
        }

        return $latestArtifactVersion.ToString(3)
    }

    $currentVersion = $latestArtifactVersion
    $defaultBuildVersion = Get-DefaultBuildVersion
    if ($null -eq $currentVersion -or $defaultBuildVersion -gt $currentVersion)
    {
        $currentVersion = $defaultBuildVersion
    }

    if ($null -eq $currentVersion)
    {
        $currentVersion = [Version]"1.0.0"
    }

    return (Get-IncrementedReleaseVersion -CurrentVersion $currentVersion -Increment $Increment).ToString(3)
}

function Invoke-BuildAndPublish
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
        Write-Host "Publishing Focus $Version..."

        & dotnet publish Systems.Sanity.Focus.csproj -c Release --self-contained -r win-x64 -o .\publish
        if ($LASTEXITCODE)
        {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }

        & vpk pack -u Focus -v $Version -p .\publish -e Systems.Sanity.Focus.exe
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

function Resolve-WinScpExecutablePath
{
    $candidatePaths = [Collections.Generic.List[string]]::new()

    foreach ($commandName in @("WinSCP.com", "WinSCP.exe"))
    {
        $commandInfo = Get-Command $commandName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $commandInfo)
        {
            continue
        }

        $commandPath = $commandInfo.Source
        $commandDirectory = Split-Path -Parent $commandPath

        if ($commandName -ieq "WinSCP.com")
        {
            $candidatePaths.Add($commandPath)
        }
        else
        {
            $candidatePaths.Add((Join-Path $commandDirectory "WinSCP.com"))
            $candidatePaths.Add($commandPath)
        }
    }

    foreach ($path in @(
        "C:\Program Files (x86)\WinSCP\WinSCP.com",
        "C:\Program Files\WinSCP\WinSCP.com",
        "C:\Program Files (x86)\WinSCP\WinSCP.exe",
        "C:\Program Files\WinSCP\WinSCP.exe"
    ))
    {
        $candidatePaths.Add($path)
    }

    $executablePath = $candidatePaths |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($executablePath))
    {
        throw "WinSCP is required for release uploads. Install WinSCP and make sure WinSCP.com is available."
    }

    return $executablePath
}

function Resolve-RemoteEndpoint
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl
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
        throw "Do not include credentials in '$normalizedBaseUrl'. Use the credential file instead."
    }

    $scheme = $uri.Scheme.ToLowerInvariant()
    if ($scheme -notin @("ftp", "ftps", "ftpes", "sftp"))
    {
        throw "Remote base URL must use ftp://, ftps://, ftpes://, or sftp://."
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

function Save-FocusReleaseCredential
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$CredentialFilePath
    )

    $credentialDirectory = Split-Path -Parent $CredentialFilePath
    if (-not [string]::IsNullOrWhiteSpace($credentialDirectory) -and -not (Test-Path -LiteralPath $credentialDirectory -PathType Container))
    {
        [void](New-Item -ItemType Directory -Path $credentialDirectory -Force)
    }

    $credential = Get-Credential -Message "Enter upload credentials for Focus release publishing"
    if ($null -eq $credential)
    {
        throw "Credential capture was cancelled."
    }

    $credential | Export-Clixml -LiteralPath $CredentialFilePath
    Write-Host "Saved upload credential to '$CredentialFilePath'."

    return $credential
}

function Get-FocusReleaseCredential
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$CredentialFilePath,
        [switch]$UpdateCredential
    )

    if ($UpdateCredential)
    {
        return Save-FocusReleaseCredential -CredentialFilePath $CredentialFilePath
    }

    if (-not (Test-Path -LiteralPath $CredentialFilePath -PathType Leaf))
    {
        Write-Host "Upload credential was not found at '$CredentialFilePath'."
        return Save-FocusReleaseCredential -CredentialFilePath $CredentialFilePath
    }

    $credential = Import-Clixml -LiteralPath $CredentialFilePath
    if ($credential -isnot [Management.Automation.PSCredential])
    {
        throw "Credential file '$CredentialFilePath' does not contain a PSCredential."
    }

    return $credential
}

function Get-LocalReleaseFiles
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseDirectoryPath
    )

    if (-not (Test-Path -LiteralPath $ReleaseDirectoryPath -PathType Container))
    {
        throw "Releases directory '$ReleaseDirectoryPath' was not found."
    }

    $releaseFiles = Get-ChildItem -LiteralPath $ReleaseDirectoryPath -File | Sort-Object Name
    if (-not $releaseFiles)
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain any files."
    }

    $requiredExactNames = @(
        "RELEASES",
        "releases.win.json",
        "assets.win.json"
    )

    foreach ($requiredExactName in $requiredExactNames)
    {
        if (-not ($releaseFiles.Name -contains $requiredExactName))
        {
            throw "Releases directory '$ReleaseDirectoryPath' is missing required artifact '$requiredExactName'."
        }
    }

    if (-not ($releaseFiles.Name | Where-Object { $_ -like "*.nupkg" }))
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain any .nupkg package."
    }

    if (-not ($releaseFiles.Name | Where-Object { $_ -like "*-Setup.exe" }))
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain a setup executable."
    }

    if (-not ($releaseFiles.Name | Where-Object { $_ -like "*-Portable.zip" }))
    {
        throw "Releases directory '$ReleaseDirectoryPath' does not contain a portable zip."
    }

    return $releaseFiles
}

function Get-UploadOrderedReleaseFiles
{
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$LocalReleaseFiles
    )

    $metadataFileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($metadataFileName in @("RELEASES", "releases.win.json"))
    {
        [void]$metadataFileNames.Add($metadataFileName)
    }

    $payloadFiles = @()
    $metadataFiles = @()

    foreach ($localReleaseFile in $LocalReleaseFiles)
    {
        if ($metadataFileNames.Contains($localReleaseFile.Name))
        {
            $metadataFiles += $localReleaseFile
        }
        else
        {
            $payloadFiles += $localReleaseFile
        }
    }

    return @(
        $payloadFiles | Sort-Object Name
        $metadataFiles | Sort-Object Name
    )
}

function Get-ReleaseSyncPlan
{
    param(
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

    $filesToDelete = $RemoteFileNames |
        Where-Object { -not $localFileNames.Contains($_) } |
        Sort-Object

    [PSCustomObject]@{
        UploadFiles     = @(Get-UploadOrderedReleaseFiles -LocalReleaseFiles $LocalReleaseFiles)
        DeleteFileNames = @($filesToDelete)
    }
}

function ConvertTo-WinScpQuotedValue
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    $escapedValue = $Value.Replace('"', '""').Replace("`r", "").Replace("`n", "")
    return '"' + $escapedValue + '"'
}

function Get-WinScpSessionProtocol
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit"
    )

    switch ($RemoteEndpoint.Scheme)
    {
        "ftpes" { return "ftpes" }
        "ftps"
        {
            if ($FtpsMode -eq "Implicit")
            {
                return "ftps"
            }

            return "ftpes"
        }
        default { return $RemoteEndpoint.Scheme }
    }
}

function New-WinScpSessionUrl
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit"
    )

    $scheme = Get-WinScpSessionProtocol -RemoteEndpoint $RemoteEndpoint -FtpsMode $FtpsMode

    $sessionUrl = [System.Text.StringBuilder]::new()
    [void]$sessionUrl.Append($scheme)
    [void]$sessionUrl.Append("://")

    $hostName = $RemoteEndpoint.Uri.Host
    if ($hostName.Contains(":") -and -not $hostName.StartsWith("["))
    {
        [void]$sessionUrl.Append("[")
        [void]$sessionUrl.Append($hostName)
        [void]$sessionUrl.Append("]")
    }
    else
    {
        [void]$sessionUrl.Append($hostName)
    }

    if (-not $RemoteEndpoint.Uri.IsDefaultPort -and $RemoteEndpoint.Uri.Port -gt 0)
    {
        [void]$sessionUrl.Append(":")
        [void]$sessionUrl.Append($RemoteEndpoint.Uri.Port)
    }

    [void]$sessionUrl.Append("/")
    return $sessionUrl.ToString()
}

function New-WinScpOpenCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [Parameter(Mandatory = $true)]
        [Management.Automation.PSCredential]$Credential,
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit",
        [string]$SshHostKeyFingerprint,
        [string]$TlsHostCertificateFingerprint
    )

    $commandParts = [Collections.Generic.List[string]]::new()
    $commandParts.Add("open")
    $commandParts.Add((New-WinScpSessionUrl -RemoteEndpoint $RemoteEndpoint -FtpsMode $FtpsMode))
    $commandParts.Add(('-username={0}' -f (ConvertTo-WinScpQuotedValue -Value $Credential.UserName)))

    $plainTextPassword = $Credential.GetNetworkCredential().Password
    $commandParts.Add(('-password={0}' -f (ConvertTo-WinScpQuotedValue -Value $plainTextPassword)))

    switch ($RemoteEndpoint.Scheme)
    {
        "sftp"
        {
            if (-not [string]::IsNullOrWhiteSpace($TlsHostCertificateFingerprint))
            {
                throw "-TlsHostCertificateFingerprint can only be used with FTPS endpoints."
            }

            $hostKeyValue = if ([string]::IsNullOrWhiteSpace($SshHostKeyFingerprint)) { "acceptnew" } else { $SshHostKeyFingerprint.Trim() }
            $commandParts.Add(('-hostkey={0}' -f (ConvertTo-WinScpQuotedValue -Value $hostKeyValue)))
            break
        }
        "ftp"
        {
            if (-not [string]::IsNullOrWhiteSpace($SshHostKeyFingerprint))
            {
                throw "-SshHostKeyFingerprint can only be used with SFTP endpoints."
            }

            if (-not [string]::IsNullOrWhiteSpace($TlsHostCertificateFingerprint))
            {
                throw "-TlsHostCertificateFingerprint can only be used with FTPS endpoints."
            }

            break
        }
        "ftpes"
        {
            if (-not [string]::IsNullOrWhiteSpace($SshHostKeyFingerprint))
            {
                throw "-SshHostKeyFingerprint can only be used with SFTP endpoints."
            }

            if (-not [string]::IsNullOrWhiteSpace($TlsHostCertificateFingerprint))
            {
                $commandParts.Add(('-certificate={0}' -f (ConvertTo-WinScpQuotedValue -Value $TlsHostCertificateFingerprint.Trim())))
            }

            break
        }
        "ftps"
        {
            if (-not [string]::IsNullOrWhiteSpace($SshHostKeyFingerprint))
            {
                throw "-SshHostKeyFingerprint can only be used with SFTP endpoints."
            }

            if (-not [string]::IsNullOrWhiteSpace($TlsHostCertificateFingerprint))
            {
                $commandParts.Add(('-certificate={0}' -f (ConvertTo-WinScpQuotedValue -Value $TlsHostCertificateFingerprint.Trim())))
            }

            break
        }
        default
        {
            throw "Unsupported remote URL scheme '$($RemoteEndpoint.Scheme)'."
        }
    }

    return ($commandParts -join " ")
}

function Get-WinScpFailureMessages
{
    param(
        [xml]$XmlDocument
    )

    if ($null -eq $XmlDocument)
    {
        return @()
    }

    return @(
        $XmlDocument.SelectNodes('//*[local-name()="failure"]/*[local-name()="message"]') |
            ForEach-Object { $_.InnerText.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
}

function Remove-WinScpSensitiveValues
{
    param(
        [AllowEmptyString()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return $Value
    }

    return [regex]::Replace($Value, '(?i)(-password=)"[^"]*"', '$1"***"')
}

function Format-WinScpFailureMessage
{
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,
        [string]$StandardOutput,
        [string]$StandardError,
        [xml]$XmlDocument
    )

    $messageParts = [Collections.Generic.List[string]]::new()
    foreach ($message in (Get-WinScpFailureMessages -XmlDocument $XmlDocument))
    {
        $messageParts.Add($message)
    }

    foreach ($message in @($StandardError, $StandardOutput))
    {
        if ([string]::IsNullOrWhiteSpace($message))
        {
            continue
        }

        $trimmedMessage = (Remove-WinScpSensitiveValues -Value $message).Trim()
        if (-not $messageParts.Contains($trimmedMessage))
        {
            $messageParts.Add($trimmedMessage)
        }
    }

    if ($messageParts.Count -eq 0)
    {
        return "WinSCP exited with code $ExitCode."
    }

    return "WinSCP exited with code ${ExitCode}: $($messageParts -join ' | ')"
}

function Invoke-WinScpCommands
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Commands
    )

    $xmlLogPath = Join-Path ([IO.Path]::GetTempPath()) ("focus-winscp-{0}.xml" -f [Guid]::NewGuid().ToString("N"))
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $ExecutablePath
    $startInfo.Arguments = ('/ini=nul /xmllog={0} /xmlgroups /nointeractiveinput' -f (ConvertTo-WinScpQuotedValue -Value $xmlLogPath))
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo

    try
    {
        [void]$process.Start()

        foreach ($commandLine in @("option batch abort", "option confirm off") + $Commands + @("exit"))
        {
            $process.StandardInput.WriteLine($commandLine)
        }

        $process.StandardInput.Close()

        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()

        $process.WaitForExit()

        $xmlDocument = $null
        if (Test-Path -LiteralPath $xmlLogPath -PathType Leaf)
        {
            [xml]$xmlDocument = Get-Content -LiteralPath $xmlLogPath -Raw
        }

        if ($process.ExitCode -ne 0)
        {
            throw (Format-WinScpFailureMessage `
                -ExitCode $process.ExitCode `
                -StandardOutput $standardOutput `
                -StandardError $standardError `
                -XmlDocument $xmlDocument)
        }

        return [PSCustomObject]@{
            StandardOutput = $standardOutput
            StandardError  = $standardError
            XmlDocument    = $xmlDocument
        }
    }
    finally
    {
        if (Test-Path -LiteralPath $xmlLogPath -PathType Leaf)
        {
            Remove-Item -LiteralPath $xmlLogPath -Force
        }

        $process.Dispose()
    }
}

function Get-RemoteFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [xml]$XmlDocument
    )

    $fileNodes = $XmlDocument.SelectNodes(
        '//*[local-name()="ls"][*[local-name()="result"][@success="true"]]/*[local-name()="files"]/*[local-name()="file"]'
    )

    return @(
        $fileNodes |
            ForEach-Object {
                $typeNode = $_.SelectSingleNode('*[local-name()="type"]')
                $fileNameNode = $_.SelectSingleNode('*[local-name()="filename"]')
                if ($null -eq $fileNameNode -or $null -eq $fileNameNode.Attributes["value"])
                {
                    return
                }

                $fileName = $fileNameNode.Attributes["value"].Value
                if ($fileName -in @(".", ".."))
                {
                    return
                }

                if ($null -ne $typeNode -and $null -ne $typeNode.Attributes["value"] -and $typeNode.Attributes["value"].Value -eq "d")
                {
                    return
                }

                $fileName
            } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )
}

function Get-RemoteReleaseFileNames
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [Parameter(Mandatory = $true)]
        [Management.Automation.PSCredential]$Credential,
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit",
        [string]$SshHostKeyFingerprint,
        [string]$TlsHostCertificateFingerprint
    )

    $openCommand = New-WinScpOpenCommand `
        -RemoteEndpoint $RemoteEndpoint `
        -Credential $Credential `
        -FtpsMode $FtpsMode `
        -SshHostKeyFingerprint $SshHostKeyFingerprint `
        -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint

    $result = Invoke-WinScpCommands -ExecutablePath $ExecutablePath -Commands @(
        $openCommand
        ('ls {0}' -f (ConvertTo-WinScpQuotedValue -Value $RemoteEndpoint.RemoteDirectory))
    )

    return @(Get-RemoteFileNames -XmlDocument $result.XmlDocument)
}

function Invoke-RemoteReleaseChanges
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [psobject]$RemoteEndpoint,
        [Parameter(Mandatory = $true)]
        [Management.Automation.PSCredential]$Credential,
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$UploadFiles,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$DeleteFileNames,
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit",
        [string]$SshHostKeyFingerprint,
        [string]$TlsHostCertificateFingerprint
    )

    $commands = [Collections.Generic.List[string]]::new()
    $commands.Add((New-WinScpOpenCommand `
        -RemoteEndpoint $RemoteEndpoint `
        -Credential $Credential `
        -FtpsMode $FtpsMode `
        -SshHostKeyFingerprint $SshHostKeyFingerprint `
        -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint))
    $commands.Add(('cd {0}' -f (ConvertTo-WinScpQuotedValue -Value $RemoteEndpoint.RemoteDirectory)))

    foreach ($uploadFile in $UploadFiles)
    {
        $commands.Add((
            'put -transfer=binary -nopreservetime -nopermissions {0} {1}' -f
            (ConvertTo-WinScpQuotedValue -Value $uploadFile.FullName),
            (ConvertTo-WinScpQuotedValue -Value $uploadFile.Name)
        ))
    }

    foreach ($deleteFileName in $DeleteFileNames)
    {
        $commands.Add(('rm {0}' -f (ConvertTo-WinScpQuotedValue -Value $deleteFileName)))
    }

    [void](Invoke-WinScpCommands -ExecutablePath $ExecutablePath -Commands $commands.ToArray())
}

function Invoke-ReleaseAndUpload
{
    param(
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$RemoteBaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$CredentialPath,
        [ValidateSet("Patch", "Minor", "Major")]
        [string]$Increment = "Patch",
        [ValidateSet("Explicit", "Implicit")]
        [string]$FtpsMode = "Explicit",
        [string]$SshHostKeyFingerprint,
        [string]$TlsHostCertificateFingerprint,
        [switch]$UpdateCredential,
        [switch]$SkipBuild,
        [switch]$DryRun
    )

    $releaseVersion = Resolve-ReleaseVersionString -Version $Version -Increment $Increment -SkipBuild:$SkipBuild
    Write-Host "Release version: $releaseVersion"

    $remoteEndpoint = Resolve-RemoteEndpoint -BaseUrl $RemoteBaseUrl
    $winScpExecutablePath = Resolve-WinScpExecutablePath

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        if (-not $SkipBuild)
        {
            Invoke-BuildAndPublish -Version $releaseVersion
        }

        $localReleaseFiles = Get-LocalReleaseFiles -ReleaseDirectoryPath (Get-ReleasesDirectoryPath)
        $credential = Get-FocusReleaseCredential -CredentialFilePath $CredentialPath -UpdateCredential:$UpdateCredential

        if ($remoteEndpoint.Scheme -eq "sftp" -and [string]::IsNullOrWhiteSpace($SshHostKeyFingerprint))
        {
            Write-Host "SFTP host keys will be accepted automatically on first connection by WinSCP."
        }

        Write-Host "Listing remote files in '$($remoteEndpoint.RemoteDirectory)'..."
        $remoteFileNames = Get-RemoteReleaseFileNames `
            -ExecutablePath $winScpExecutablePath `
            -RemoteEndpoint $remoteEndpoint `
            -Credential $credential `
            -FtpsMode $FtpsMode `
            -SshHostKeyFingerprint $SshHostKeyFingerprint `
            -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint

        $syncPlan = Get-ReleaseSyncPlan -LocalReleaseFiles $localReleaseFiles -RemoteFileNames $remoteFileNames

        Write-Host ""
        Write-Host "Upload plan:"
        foreach ($uploadFile in $syncPlan.UploadFiles)
        {
            Write-Host "  upload $($uploadFile.Name)"
        }

        Write-Host ""
        Write-Host "Delete plan:"
        if ($syncPlan.DeleteFileNames.Count -eq 0)
        {
            Write-Host "  nothing to delete"
        }
        else
        {
            foreach ($deleteFileName in $syncPlan.DeleteFileNames)
            {
                Write-Host "  delete $deleteFileName"
            }
        }

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
            -ExecutablePath $winScpExecutablePath `
            -RemoteEndpoint $remoteEndpoint `
            -Credential $credential `
            -UploadFiles $syncPlan.UploadFiles `
            -DeleteFileNames $syncPlan.DeleteFileNames `
            -FtpsMode $FtpsMode `
            -SshHostKeyFingerprint $SshHostKeyFingerprint `
            -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint

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
        Invoke-ReleaseAndUpload `
            -Version $Version `
            -RemoteBaseUrl $RemoteBaseUrl `
            -CredentialPath $CredentialPath `
            -Increment $Increment `
            -FtpsMode $FtpsMode `
            -SshHostKeyFingerprint $SshHostKeyFingerprint `
            -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint `
            -UpdateCredential:$UpdateCredential `
            -SkipBuild:$SkipBuild `
            -DryRun:$DryRun
    }
    catch
    {
        Write-Error $_
        exit 1
    }
}
