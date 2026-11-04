param(
    [int]$Port = 8080
)

$ErrorActionPreference = 'Stop'

# 已经有服务端监听时直接复用，避免重复启动。
$existing = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Unity MCP server is already listening on 127.0.0.1:$Port"
    exit 0
}

# 优先复用 uv 缓存中的 mcp-for-unity，启动更快且可离线使用。
$cached = Get-ChildItem "$env:LOCALAPPDATA\uv\cache\archive-v0" -Recurse -Filter 'mcp-for-unity.exe' `
    -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($cached) {
    Start-Process -FilePath $cached.FullName -ArgumentList @(
        '--transport', 'http',
        '--http-host', '127.0.0.1',
        '--http-port', $Port
    ) -WindowStyle Hidden

    Start-Sleep -Seconds 3
    if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
        Write-Host "Unity MCP server started from cache (PID child of $($cached.FullName))"
        exit 0
    }
}

Write-Host "Starting Unity MCP server via uvx..."
Start-Process -FilePath 'uvx' -ArgumentList @(
    '--from', 'mcpforunityserver==9.7.1',
    'mcp-for-unity',
    '--transport', 'http',
    '--http-host', '127.0.0.1',
    '--http-port', $Port
) -WindowStyle Hidden

Start-Sleep -Seconds 5
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
    Write-Host "Unity MCP server started on 127.0.0.1:$Port"
} else {
    Write-Warning "Server did not open port $Port. Check that uv/uvx is on PATH."
    exit 1
}
