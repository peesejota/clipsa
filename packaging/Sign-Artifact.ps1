[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$FilePath)
$ErrorActionPreference = 'Stop'
$thumbprint = $env:CLIPSA_SIGN_CERT_THUMBPRINT -replace '\s', ''
if ($thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'CLIPSA_SIGN_CERT_THUMBPRINT must identify an accessible code-signing certificate (40 hexadecimal characters).' }
$signTool = $env:CLIPSA_SIGNTOOL_PATH
if (!$signTool -or !(Test-Path -LiteralPath $signTool -PathType Leaf)) { throw 'Set CLIPSA_SIGNTOOL_PATH to the local signtool.exe.' }
$file = (Resolve-Path -LiteralPath $FilePath).Path
$arguments = @('sign', '/sha1', $thumbprint, '/s', 'My', '/fd', 'SHA256')
if ($env:CLIPSA_SIGN_CERT_STORE -eq 'LocalMachine') { $arguments += '/sm' }
elseif ($env:CLIPSA_SIGN_CERT_STORE -and $env:CLIPSA_SIGN_CERT_STORE -ne 'CurrentUser') { throw 'CLIPSA_SIGN_CERT_STORE must be CurrentUser or LocalMachine.' }
if ($env:CLIPSA_TIMESTAMP_URL) {
    $uri = $null
    if (![Uri]::TryCreate($env:CLIPSA_TIMESTAMP_URL, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo) { throw 'Timestamp URL must be HTTPS without credentials.' }
    $arguments += @('/tr', $env:CLIPSA_TIMESTAMP_URL, '/td', 'SHA256')
}
& $signTool @arguments $file
if ($LASTEXITCODE -ne 0) { throw "SignTool signing failed: $LASTEXITCODE" }
& $signTool verify /pa /v $file
if ($LASTEXITCODE -ne 0) { throw "SignTool verification failed: $LASTEXITCODE" }
