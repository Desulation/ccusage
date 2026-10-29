<#
.SYNOPSIS
Smoke-tests a ccusage.mcpb through its manifest command on the current OS.

.DESCRIPTION
Extracts the bundle (unzip on Linux/macOS so unix modes survive; ZipFile on Windows), asserts the
exec bits of this platform's server and engine on Linux/macOS (spec §15 row 4, Q4), starts the
manifest's command for this OS with ${__dirname} replaced by the extraction directory, and talks
newline-delimited JSON-RPC to it over stdio: initialize, notifications/initialized, tools/list,
tools/call list-sources and tools/call daily. Each reply is read before the next request is written
and stdin is closed only after the last reply, because stdin EOF shuts the stdio transport down
before pending replies are written (spec §15 environment notes). list-sources must report an
engine-path ending in -ExpectEnginePath (Q5 on Windows ARM64: win\arm64\ccusage.exe).

Prints "bundle-smoke: ok" and exits 0, or prints the failing step and exits 1.

.EXAMPLE
pwsh -NoProfile -File dotnet/packaging/bundle-smoke.ps1 -Bundle dist/ccusage.mcpb -FixturesDir dotnet/tests/fixtures -ExpectEnginePath 'win\x64\ccusage.exe'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Bundle,
    [Parameter(Mandatory)] [string] $FixturesDir,
    [Parameter(Mandatory)] [string] $ExpectEnginePath,
    [int] $ReplyTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'

$script:step = 'setup'
$script:proc = $null
$script:stderrTask = $null
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("ccusage-bundle-smoke-" + [guid]::NewGuid().ToString('N'))

function Fail([string] $message) {
    throw $message
}

function Send-Message([System.Collections.IDictionary] $message) {
    $json = $message | ConvertTo-Json -Compress -Depth 20
    $script:proc.StandardInput.Write($json + "`n")
    $script:proc.StandardInput.Flush()
}

# Reads stdout lines until the JSON-RPC response with this id arrives (server notifications and
# requests without a matching id are skipped).
function Read-Reply([int] $id) {
    $deadline = [DateTime]::UtcNow.AddSeconds($ReplyTimeoutSeconds)
    while ($true) {
        $remaining = $deadline - [DateTime]::UtcNow
        if ($remaining -le [TimeSpan]::Zero) { Fail "no reply with id $id within $ReplyTimeoutSeconds s" }
        $task = $script:proc.StandardOutput.ReadLineAsync()
        if (-not $task.Wait($remaining)) { Fail "no reply with id $id within $ReplyTimeoutSeconds s" }
        $line = $task.Result
        if ($null -eq $line) { Fail "server closed stdout before the reply with id $id" }
        if ($line.Trim().Length -eq 0) { continue }
        try { $msg = $line | ConvertFrom-Json } catch { Fail "stdout line is not JSON: $line" }
        $idProp = $msg.PSObject.Properties['id']
        if ($null -eq $idProp -or $null -eq $idProp.Value -or [string]$idProp.Value -ne [string]$id) { continue }
        if ($null -ne $msg.PSObject.Properties['error']) {
            Fail "JSON-RPC error: $($msg.error | ConvertTo-Json -Compress -Depth 20)"
        }
        if ($null -eq $msg.PSObject.Properties['result']) { Fail "reply has no result: $line" }
        return $msg.result
    }
}

function Assert-ToolResultOk($result, [string] $tool) {
    $isError = $result.PSObject.Properties['isError']
    if ($null -ne $isError -and $isError.Value -eq $true) {
        Fail "$tool returned isError: $(@($result.content)[0].text)"
    }
    $blocks = @($result.content)
    if ($blocks.Count -lt 1 -or $null -eq $blocks[0].text) { Fail "$tool returned no text block" }
    return [string]$blocks[0].text
}

$exitCode = 1
try {
    # --- extract --------------------------------------------------------------------------------
    $script:step = 'extract'
    if (-not (Test-Path -LiteralPath $Bundle -PathType Leaf)) { Fail "bundle not found: $Bundle" }
    $bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
    $fixturesPath = (Resolve-Path -LiteralPath $FixturesDir).Path
    $claudeConfig = Join-Path $fixturesPath 'claude'
    if (-not (Test-Path -LiteralPath $claudeConfig -PathType Container)) { Fail "fixtures dir has no claude/: $fixturesPath" }

    $extractDir = Join-Path $work 'bundle'
    $homeDir = Join-Path $work 'home'
    New-Item -ItemType Directory -Force -Path $extractDir, $homeDir | Out-Null

    if ($IsWindows) {
        # Expand-Archive refuses a file whose extension is not .zip, so use ZipFile directly.
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($bundlePath, $extractDir)
    }
    else {
        # unzip restores the unix modes Info-ZIP stored.
        & unzip -q $bundlePath -d $extractDir
        if ($LASTEXITCODE -ne 0) { Fail "unzip exited $LASTEXITCODE" }
    }

    # --- exec bits (Q4) -------------------------------------------------------------------------
    if (-not $IsWindows) {
        $script:step = 'exec-bits'
        $os = if ($IsMacOS) { 'osx' } else { 'linux' }
        $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
            'X64' { 'x64' }
            'Arm64' { 'arm64' }
            default { Fail "unsupported architecture: $_" }
        }
        foreach ($name in 'ccusage-mcp', 'ccusage') {
            $path = Join-Path $extractDir "$os/$arch/$name"
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "missing $os/$arch/$name" }
            $mode = (Get-Item -LiteralPath $path).UnixMode
            if ($mode.Length -lt 4 -or $mode[3] -ne 'x') { Fail "$os/$arch/$name is not executable after extraction (mode $mode)" }
            Write-Host "bundle-smoke: $os/$arch/$name mode $mode"
        }
    }

    # --- manifest command -----------------------------------------------------------------------
    $script:step = 'manifest'
    $manifestPath = Join-Path $extractDir 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { Fail 'manifest.json missing from bundle' }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $config = if ($IsWindows) { $manifest.server.mcp_config.platform_overrides.win32 } else { $manifest.server.mcp_config }
    if ($null -eq $config -or [string]::IsNullOrEmpty($config.command)) { Fail 'no mcp_config command for this platform' }
    $command = ([string]$config.command).Replace('${__dirname}', $extractDir)
    $arguments = @(@($config.args) | Where-Object { $null -ne $_ } | ForEach-Object { ([string]$_).Replace('${__dirname}', $extractDir) })
    Write-Host "bundle-smoke: starting $command $($arguments -join ' ')"

    # --- start ----------------------------------------------------------------------------------
    $script:step = 'start'
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $psi = [System.Diagnostics.ProcessStartInfo]::new($command)
    foreach ($a in $arguments) { $psi.ArgumentList.Add($a) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardInputEncoding = $utf8
    $psi.StandardOutputEncoding = $utf8
    $psi.StandardErrorEncoding = $utf8
    $psi.WorkingDirectory = $work
    $psi.Environment['CLAUDE_CONFIG_DIR'] = $claudeConfig
    $psi.Environment['HOME'] = $homeDir
    $psi.Environment['USERPROFILE'] = $homeDir
    # An inherited CCUSAGE_ENGINE would bypass the bundled engine the Q5 check is about.
    [void]$psi.Environment.Remove('CCUSAGE_ENGINE')
    $script:proc = [System.Diagnostics.Process]::Start($psi)
    $script:stderrTask = $script:proc.StandardError.ReadToEndAsync()

    # --- initialize -----------------------------------------------------------------------------
    $script:step = 'initialize'
    Send-Message ([ordered]@{
            jsonrpc = '2.0'; id = 1; method = 'initialize'
            params  = [ordered]@{
                protocolVersion = '2025-06-18'
                capabilities    = @{}
                clientInfo      = [ordered]@{ name = 'bundle-smoke'; version = '1.0.0' }
            }
        })
    $init = Read-Reply 1
    if ($init.serverInfo.name -ne 'ccusage') { Fail "serverInfo.name is '$($init.serverInfo.name)', expected 'ccusage'" }
    Write-Host "bundle-smoke: serverInfo $($init.serverInfo.name) $($init.serverInfo.version)"

    $script:step = 'notifications/initialized'
    Send-Message ([ordered]@{ jsonrpc = '2.0'; method = 'notifications/initialized' })

    # --- tools/list -----------------------------------------------------------------------------
    $script:step = 'tools/list'
    Send-Message ([ordered]@{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} })
    $tools = @((Read-Reply 2).tools)
    if ($tools.Count -ne 8) { Fail "expected 8 tools, got $($tools.Count): $(($tools | ForEach-Object name) -join ', ')" }

    # --- list-sources (Q5) ----------------------------------------------------------------------
    $script:step = 'tools/call list-sources'
    Send-Message ([ordered]@{
            jsonrpc = '2.0'; id = 3; method = 'tools/call'
            params  = [ordered]@{ name = 'list-sources'; arguments = @{} }
        })
    $text = Assert-ToolResultOk (Read-Reply 3) 'list-sources'
    try { $sources = $text | ConvertFrom-Json } catch { Fail "list-sources text is not JSON: $text" }
    $engineError = $sources.PSObject.Properties['engine-error']
    $errorSuffix = if ($null -ne $engineError) { " (engine-error: $($engineError.Value))" } else { '' }
    if ($null -eq $sources.'engine-version') { Fail "engine-version is null$errorSuffix" }
    $enginePath = [string]$sources.'engine-path'
    if ([string]::IsNullOrEmpty($enginePath)) { Fail "engine-path is null$errorSuffix" }
    # The server may mix separators (the manifest uses '/'); compare with both normalized.
    $normalizedPath = $enginePath.Replace('\', '/')
    $normalizedExpect = $ExpectEnginePath.Replace('\', '/')
    if (-not $normalizedPath.EndsWith('/' + $normalizedExpect.TrimStart('/'), [StringComparison]::OrdinalIgnoreCase)) {
        Fail "engine-path '$enginePath' does not end with '$ExpectEnginePath'"
    }
    Write-Host "bundle-smoke: engine $($sources.'engine-version') at $enginePath"

    # --- daily ----------------------------------------------------------------------------------
    $script:step = 'tools/call daily'
    Send-Message ([ordered]@{
            jsonrpc = '2.0'; id = 4; method = 'tools/call'
            params  = [ordered]@{ name = 'daily'; arguments = [ordered]@{ source = 'claude'; offline = $true } }
        })
    $text = Assert-ToolResultOk (Read-Reply 4) 'daily'
    try { $null = $text | ConvertFrom-Json } catch { Fail "daily block 0 is not JSON: $text" }
    Write-Host "bundle-smoke: daily returned $($text.Length) chars of JSON"

    # --- shutdown -------------------------------------------------------------------------------
    $script:step = 'shutdown'
    $script:proc.StandardInput.Close()
    if (-not $script:proc.WaitForExit(10000)) { Write-Host 'bundle-smoke: server did not exit within 10 s after stdin closed; killing it' }

    Write-Host 'bundle-smoke: ok'
    $exitCode = 0
}
catch {
    Write-Host "bundle-smoke: FAIL at $($script:step): $($_.Exception.Message)"
}
finally {
    if ($null -ne $script:proc) {
        if (-not $script:proc.HasExited) {
            try { $script:proc.Kill($true) } catch { }
            [void]$script:proc.WaitForExit(5000)
        }
        if ($exitCode -ne 0 -and $null -ne $script:stderrTask -and $script:stderrTask.Wait(2000) -and $script:stderrTask.Result) {
            Write-Host '--- server stderr ---'
            Write-Host $script:stderrTask.Result
        }
        $script:proc.Dispose()
    }
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
exit $exitCode
