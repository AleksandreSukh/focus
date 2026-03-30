param(
    [int]$Port = 4173
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$utf8 = [System.Text.Encoding]::UTF8
$headerEncoding = [System.Text.UTF8Encoding]::new($false)

function Get-ContentType([string]$path) {
    switch ([System.IO.Path]::GetExtension($path).ToLowerInvariant()) {
        '.html' { return 'text/html; charset=utf-8' }
        '.js' { return 'text/javascript; charset=utf-8' }
        '.css' { return 'text/css; charset=utf-8' }
        '.svg' { return 'image/svg+xml' }
        '.webmanifest' { return 'application/manifest+json' }
        '.json' { return 'application/json; charset=utf-8' }
        default { return 'application/octet-stream' }
    }
}

function Write-Response(
    [System.IO.Stream]$stream,
    [int]$statusCode,
    [string]$statusText,
    [byte[]]$body,
    [string]$contentType
) {
    $writer = New-Object System.IO.StreamWriter($stream, $headerEncoding, 1024, $true)
    $writer.NewLine = "`r`n"
    $writer.WriteLine("HTTP/1.1 $statusCode $statusText")
    $writer.WriteLine("Content-Type: $contentType")
    $writer.WriteLine("Content-Length: $($body.Length)")
    $writer.WriteLine('Cache-Control: no-store')
    $writer.WriteLine('Connection: close')
    $writer.WriteLine()
    $writer.Flush()
    $stream.Write($body, 0, $body.Length)
    $stream.Flush()
}

function Normalize-RequestPath([string]$rawPath) {
    $decoded = [System.Uri]::UnescapeDataString(($rawPath -split '\?')[0])
    if ([string]::IsNullOrWhiteSpace($decoded) -or $decoded -eq '/') {
        return 'index.html'
    }

    $trimmed = $decoded.TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return 'index.html'
    }

    $normalized = $trimmed -replace '/', '\'
    if ($normalized.Contains('..')) {
        throw 'Path traversal is not allowed.'
    }

    return $normalized
}

$listener.Start()
Write-Host "Serving PWA from $root"
Write-Host "Open http://127.0.0.1:$Port/"
Write-Host 'Press Ctrl+C to stop.'

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $reader = New-Object System.IO.StreamReader($stream, $utf8, $false, 1024, $true)
            $requestLine = $reader.ReadLine()
            if ([string]::IsNullOrWhiteSpace($requestLine)) {
                continue
            }

            while (($headerLine = $reader.ReadLine()) -ne '') {
                if ($null -eq $headerLine) {
                    break
                }
            }

            $parts = $requestLine.Split(' ')
            $method = if ($parts.Length -gt 0) { $parts[0] } else { 'GET' }
            $path = if ($parts.Length -gt 1) { $parts[1] } else { '/' }

            if ($method -ne 'GET') {
                $body = $utf8.GetBytes('Method not allowed')
                Write-Response -stream $stream -statusCode 405 -statusText 'Method Not Allowed' -body $body -contentType 'text/plain; charset=utf-8'
                continue
            }

            try {
                $relativePath = Normalize-RequestPath $path
            } catch {
                $body = $utf8.GetBytes($_.Exception.Message)
                Write-Response -stream $stream -statusCode 400 -statusText 'Bad Request' -body $body -contentType 'text/plain; charset=utf-8'
                continue
            }

            $localPath = Join-Path $root $relativePath
            if (-not (Test-Path -LiteralPath $localPath -PathType Leaf)) {
                $body = $utf8.GetBytes('Not found')
                Write-Response -stream $stream -statusCode 404 -statusText 'Not Found' -body $body -contentType 'text/plain; charset=utf-8'
                continue
            }

            $bytes = [System.IO.File]::ReadAllBytes($localPath)
            Write-Response -stream $stream -statusCode 200 -statusText 'OK' -body $bytes -contentType (Get-ContentType $localPath)
        } finally {
            $client.Close()
        }
    }
} finally {
    $listener.Stop()
}
