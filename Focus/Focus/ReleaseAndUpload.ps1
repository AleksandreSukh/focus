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

. (Join-Path $PSScriptRoot "ReleaseAndUpload.Shared.ps1")

function Get-WindowsPublishDirectoryPath
{
    return Join-Path $PSScriptRoot "publish"
}

function Invoke-WindowsBuildAndPublish
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$DotnetExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$VpkExecutablePath
    )

    if ([string]::IsNullOrWhiteSpace($Version))
    {
        throw "Version cannot be empty."
    }

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        Write-Host "Publishing Focus $Version for win-x64..."

        & $DotnetExecutablePath publish Systems.Sanity.Focus.csproj -c Release --self-contained -r win-x64 -o .\publish
        if ($LASTEXITCODE)
        {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }

        Assert-FocusBundledFfmpegPresent -Platform Windows -PublishDirectoryPath (Get-WindowsPublishDirectoryPath)

        & $VpkExecutablePath pack -u Focus -v $Version -p .\publish -r win-x64 -e Systems.Sanity.Focus.exe --packTitle Focus
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

    $autoVersionRequested = Test-FocusAutoVersionRequested -Version $Version
    if ($autoVersionRequested -and $SkipBuild)
    {
        throw "Cannot combine -Version Auto with -SkipBuild because -SkipBuild uploads existing artifacts without producing a new version."
    }

    $remoteEndpoint = Resolve-FocusRemoteEndpoint -BaseUrl $RemoteBaseUrl
    $credential = Get-FocusReleaseCredential -CredentialFilePath $CredentialPath -UpdateCredential:$UpdateCredential
    $dotnetExecutablePath = $null
    $vpkExecutablePath = $null
    if (-not $SkipBuild)
    {
        $dotnetExecutablePath = Resolve-RequiredExecutablePath `
            -CommandName "dotnet" `
            -FailureMessage "dotnet is required for Windows release builds. Install the .NET SDK."
        $vpkExecutablePath = Resolve-RequiredExecutablePath `
            -CommandName "vpk" `
            -FailureMessage "vpk is required for Windows release builds. Install the Velopack CLI."
        Assert-VpkExecutableReady -ExecutablePath $vpkExecutablePath
    }
    $winScpExecutablePath = Resolve-WinScpExecutablePath
    $acceptNewHostKey = $remoteEndpoint.Scheme -eq "sftp" -and [string]::IsNullOrWhiteSpace($SshHostKeyFingerprint)

    if ($autoVersionRequested)
    {
        if ($acceptNewHostKey)
        {
            Write-Host "SFTP host keys will be accepted automatically on first connection by WinSCP."
        }

        Write-Host "Resolving -Version Auto from remote files in '$($remoteEndpoint.RemoteDirectory)'..."
        $remoteVersionFileNames = Get-RemoteReleaseFileNames `
            -ExecutablePath $winScpExecutablePath `
            -RemoteEndpoint $remoteEndpoint `
            -Credential $credential `
            -FtpsMode $FtpsMode `
            -SshHostKeyFingerprint $SshHostKeyFingerprint `
            -TlsHostCertificateFingerprint $TlsHostCertificateFingerprint

        $releaseVersion = Resolve-FocusReleaseVersionString `
            -Platform Windows `
            -Version $Version `
            -Increment $Increment `
            -SkipBuild:$SkipBuild `
            -RemoteFileNames $remoteVersionFileNames `
            -VersionSourceDescription "remote release directory '$($remoteEndpoint.RemoteDirectory)'"
    }
    else
    {
        $releaseVersion = Resolve-FocusReleaseVersionString -Platform Windows -Version $Version -Increment $Increment -SkipBuild:$SkipBuild
    }

    Write-Host "Release version: $releaseVersion"

    Push-Location -LiteralPath $PSScriptRoot
    try
    {
        if (-not $SkipBuild)
        {
            Invoke-WindowsBuildAndPublish `
                -Version $releaseVersion `
                -DotnetExecutablePath $dotnetExecutablePath `
                -VpkExecutablePath $vpkExecutablePath
        }

        $localReleaseFiles = Get-FocusLocalReleaseFiles -Platform Windows -ReleaseDirectoryPath (Get-FocusReleasesDirectoryPath)

        if ($acceptNewHostKey -and -not $autoVersionRequested)
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

        $syncPlan = Get-FocusReleaseSyncPlan -Platform Windows -LocalReleaseFiles $localReleaseFiles -RemoteFileNames $remoteFileNames
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

function Invoke-ProcessWithCapturedOutput
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [string[]]$Arguments = @()
    )

    $standardOutputPath = Join-Path ([IO.Path]::GetTempPath()) ("focus-process-stdout-{0}.log" -f [Guid]::NewGuid().ToString("N"))
    $standardErrorPath = Join-Path ([IO.Path]::GetTempPath()) ("focus-process-stderr-{0}.log" -f [Guid]::NewGuid().ToString("N"))

    try
    {
        $process = Start-Process `
            -FilePath $ExecutablePath `
            -ArgumentList $Arguments `
            -Wait `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardOutput $standardOutputPath `
            -RedirectStandardError $standardErrorPath

        $standardOutput = if (Test-Path -LiteralPath $standardOutputPath -PathType Leaf)
        {
            Get-Content -LiteralPath $standardOutputPath -Raw
        }
        else
        {
            ""
        }

        $standardError = if (Test-Path -LiteralPath $standardErrorPath -PathType Leaf)
        {
            Get-Content -LiteralPath $standardErrorPath -Raw
        }
        else
        {
            ""
        }

        return [PSCustomObject]@{
            ExitCode       = $process.ExitCode
            StandardOutput = $standardOutput
            StandardError  = $standardError
        }
    }
    finally
    {
        foreach ($temporaryPath in @($standardOutputPath, $standardErrorPath))
        {
            if (Test-Path -LiteralPath $temporaryPath -PathType Leaf)
            {
                Remove-Item -LiteralPath $temporaryPath -Force
            }
        }
    }
}

function Get-ProcessFailureSummary
{
    param(
        [AllowEmptyString()]
        [string]$StandardOutput,
        [AllowEmptyString()]
        [string]$StandardError
    )

    return @($StandardError, $StandardOutput) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
}

function Assert-VpkExecutableReady
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    $probeArguments = @("--help")
    $result = Invoke-ProcessWithCapturedOutput -ExecutablePath $ExecutablePath -Arguments $probeArguments
    if ($result.ExitCode -eq 0)
    {
        return
    }

    $summaryLines = @(Get-ProcessFailureSummary -StandardOutput $result.StandardOutput -StandardError $result.StandardError)
    $summary = if ($summaryLines.Count -gt 0)
    {
        $summaryLines -join " | "
    }
    else
    {
        "No additional details were reported."
    }

    if ($summary -match 'You must install or update \.NET to run this application\.' -and $summary -match "Microsoft\.AspNetCore\.App', version '([^']+)'")
    {
        $requiredFrameworkVersion = $Matches[1]
        throw "vpk is installed at '$ExecutablePath' but cannot start because the required ASP.NET Core runtime ($requiredFrameworkVersion) is missing. Install Microsoft.AspNetCore.App $requiredFrameworkVersion x64 or update the Velopack CLI, then retry. Details: $summary"
    }

    throw "vpk is installed at '$ExecutablePath' but '$($probeArguments -join ' ')' failed with exit code $($result.ExitCode). Details: $summary"
}

if ($MyInvocation.InvocationName -ne '.')
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
