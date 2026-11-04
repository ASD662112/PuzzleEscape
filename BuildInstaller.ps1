<#
.SYNOPSIS
    一键构建 PuzzleEscape 的 Windows 版本并打成安装包。

.DESCRIPTION
    1. 用 Unity 批处理模式构建 Build\Windows\PuzzleEscape.exe
    2. 用 Inno Setup 编译 Build\Installer\PuzzleEscape_Setup.exe

    用法：
        .\BuildInstaller.cmd                 双击或命令行直接运行
        .\BuildInstaller.cmd -SkipUnity      只重新打安装包（不重新构建游戏）
        .\BuildInstaller.cmd -OpenFolder     完成后自动打开安装包所在目录

    注意：运行前请先关闭 Unity 编辑器，否则批处理构建会因项目被占用而失败。
#>

[CmdletBinding()]
param(
    [switch]$SkipUnity,
    [switch]$OpenFolder
)

$ErrorActionPreference = 'Stop'

$Root        = $PSScriptRoot
$ProjectPath = $Root
$BuildLog    = Join-Path $Root 'Build\build_windows_auto.log'
$ExePath     = Join-Path $Root 'Build\Windows\PuzzleEscape.exe'
$IssPath     = Join-Path $Root 'Build\Installer\PuzzleEscape.iss'
$SetupPath   = Join-Path $Root 'Build\Installer\PuzzleEscape_Setup.exe'

function Write-Step([string]$Text)
{
    Write-Host ''
    Write-Host ("==== " + $Text + " ====") -ForegroundColor Cyan
}

function Fail([string]$Text)
{
    Write-Host ''
    Write-Host ("[失败] " + $Text) -ForegroundColor Red
    exit 1
}

# ---------- 1. 找 Unity ----------
function Find-Unity
{
    $instance = Join-Path $Root 'Library\EditorInstance.json'
    if (Test-Path $instance)
    {
        try
        {
            $json = Get-Content $instance -Raw | ConvertFrom-Json
            $exe = $json.app_path
            if ($exe -and (Test-Path $exe))
            {
                return $exe
            }
        }
        catch { }
    }

    $candidates = @(
        'D:\Unity Hub\Unity\2022.3.62f3c1\Editor\Unity.exe',
        'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
        'D:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
    )

    foreach ($path in $candidates)
    {
        if (Test-Path $path) { return $path }
    }

    $found = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor','D:\Unity Hub\Unity' `
        -Filter Unity.exe -Recurse -Depth 3 -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($found) { return $found.FullName }
    return $null
}

# ---------- 2. 找 Inno Setup ----------
function Find-Iscc
{
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )

    foreach ($path in $candidates)
    {
        if (Test-Path $path) { return $path }
    }

    return $null
}

# ---------- 3. 检查 Unity 是否占用项目 ----------
function Get-EditorProcessId
{
    $instance = Join-Path $Root 'Library\EditorInstance.json'
    if (-not (Test-Path $instance)) { return 0 }

    try
    {
        $json = Get-Content $instance -Raw | ConvertFrom-Json
        $processId = [int]$json.process_id
        if ($processId -gt 0)
        {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($process) { return $processId }
        }
    }
    catch { }

    return 0
}

# 编辑器已打开时不能再起批处理实例（同一项目会被锁），
# 改为写请求文件，让正在运行的编辑器自己构建（Assets/Editor/BuildRequestWatcher.cs）
function Invoke-EditorBuild
{
    $buildDir = Join-Path $Root 'Build'
    $request  = Join-Path $buildDir 'build_request.flag'
    $result   = Join-Path $buildDir 'build_result.txt'

    if (-not (Test-Path $buildDir))
    {
        New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
    }

    Remove-Item $result -ErrorAction SilentlyContinue
    New-Item -ItemType File -Path $request -Force | Out-Null

    Write-Host '已发送构建请求，Unity 窗口会短暂无响应，请勿关闭编辑器……'

    $deadline = (Get-Date).AddMinutes(30)

    while ((Get-Date) -lt $deadline)
    {
        Start-Sleep -Milliseconds 800

        if (-not (Test-Path $result)) { continue }

        $text = ''
        try { $text = (Get-Content $result -Raw).Trim() } catch { continue }

        if ($text -eq '' -or $text -eq 'BUILDING') { continue }

        if ($text -eq 'OK')
        {
            Remove-Item $result -ErrorAction SilentlyContinue
            return
        }

        if ($text -like 'FAIL:*')
        {
            Remove-Item $result -ErrorAction SilentlyContinue
            Fail ('编辑器内构建失败：' + $text)
        }
    }

    Remove-Item $request -ErrorAction SilentlyContinue
    Fail '等待编辑器构建超时（30 分钟）。请确认 Unity 里没有弹窗或编译错误。'
}

# 清掉上一次的构建产物：残留或半覆盖的 levelN 文件会导致
# 「The file 'level1' is corrupted!」并在切换地图时崩溃。
function Remove-OldBuild
{
    $outDir = Split-Path $ExePath -Parent
    if (-not (Test-Path $outDir)) { return }

    Write-Step '清理旧的构建产物'
    try
    {
        Get-ChildItem -Path $outDir -Force | Remove-Item -Recurse -Force -ErrorAction Stop
    }
    catch
    {
        Fail ('无法清理 ' + $outDir + '，请先关闭正在运行的游戏再重试。')
    }
}

# ---------- 开始 ----------
Write-Host 'PuzzleEscape 一键构建：Windows 游戏 + 安装包' -ForegroundColor Green
Write-Host ("项目目录：" + $ProjectPath)

if (-not (Test-Path $IssPath))
{
    Fail ("找不到安装脚本：" + $IssPath)
}

$Iscc = Find-Iscc
if (-not $Iscc)
{
    Fail '找不到 Inno Setup（ISCC.exe）。请先安装：winget install --id JRSoftware.InnoSetup -e'
}

$StartTime = Get-Date

# ---------- 构建游戏 ----------
if ($SkipUnity)
{
    Write-Step '跳过 Unity 构建（-SkipUnity）'
    if (-not (Test-Path $ExePath))
    {
        Fail ('Build\Windows 下没有 PuzzleEscape.exe，请先去掉 -SkipUnity 完整构建一次。')
    }
}
elseif ((Get-EditorProcessId) -gt 0)
{
    Write-Step 'Unity 编辑器正在运行 → 由编辑器执行构建'
    Remove-OldBuild
    Invoke-EditorBuild

    if (-not (Test-Path $ExePath))
    {
        Fail ('构建结束但没有生成 ' + $ExePath + '，请查看 Unity Console。')
    }

    if ((Get-Item $ExePath).LastWriteTime -lt $StartTime.AddMinutes(-1))
    {
        Fail ('PuzzleEscape.exe 没有更新，构建可能被跳过，请查看 Unity Console。')
    }

    Write-Host '游戏构建完成。' -ForegroundColor Green
}
else
{
    $Unity = Find-Unity
    if (-not $Unity)
    {
        Fail '找不到 Unity.exe，请手动修改脚本里的候选路径。'
    }

    Write-Step 'Unity 批处理构建 Windows x64'
    Write-Host ("Unity：" + $Unity)

    Remove-OldBuild

    $outDir = Split-Path $ExePath -Parent
    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

    $unityArgs = @(
        '-batchmode',
        '-quit',
        '-projectPath', $ProjectPath,
        '-buildTarget', 'StandaloneWindows64',
        '-executeMethod', 'PuzzleEscape.EditorTools.BatchBuild.BuildWindows',
        '-logFile', $BuildLog
    )

    Write-Host '正在构建，可能需要几分钟，请勿关闭窗口……'
    $process = Start-Process -FilePath $Unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    Write-Host ("Unity 退出码：" + $process.ExitCode)

    if (Test-Path $BuildLog)
    {
        $resultLine = Select-String -Path $BuildLog -Pattern '构建结果|构建失败|error CS|IL Post Processor' |
            Select-Object -Last 5
        if ($resultLine)
        {
            Write-Host '--- 构建日志摘要 ---' -ForegroundColor DarkGray
            $resultLine | ForEach-Object { Write-Host ('  ' + $_.Line) -ForegroundColor DarkGray }
        }
    }

    if ($process.ExitCode -ne 0)
    {
        Fail ('Unity 构建失败，详见日志：' + $BuildLog)
    }

    if (-not (Test-Path $ExePath))
    {
        Fail ('构建结束但没有生成 ' + $ExePath + '，详见日志：' + $BuildLog)
    }

    if ((Get-Item $ExePath).LastWriteTime -lt $StartTime.AddMinutes(-1))
    {
        Fail ('PuzzleEscape.exe 没有更新，构建可能被跳过，详见日志：' + $BuildLog)
    }

    Write-Host '游戏构建完成。' -ForegroundColor Green
}

# ---------- 打安装包 ----------
Write-Step '编译安装包（Inno Setup）'
Write-Host ("ISCC：" + $Iscc)

# 旧安装包若正被打开/预览会锁文件，先尝试删掉
if (Test-Path $SetupPath)
{
    $removed = $false
    for ($i = 0; $i -lt 5 -and -not $removed; $i++)
    {
        try
        {
            Remove-Item $SetupPath -Force -ErrorAction Stop
            $removed = $true
        }
        catch
        {
            Start-Sleep -Milliseconds 400
        }
    }

    if (-not $removed)
    {
        Fail ('旧的安装包正被占用，无法覆盖：' + $SetupPath + '。请关闭正在运行的安装程序或资源管理器预览窗口后重试。')
    }
}

& $Iscc $IssPath | ForEach-Object { Write-Host ('  ' + $_) -ForegroundColor DarkGray }
if ($LASTEXITCODE -ne 0)
{
    Fail '安装包编译失败，请查看上面的 Inno Setup 输出。'
}

if (-not (Test-Path $SetupPath))
{
    Fail ('没有生成安装包：' + $SetupPath)
}

$setup = Get-Item $SetupPath
Write-Host ''
Write-Host '全部完成！' -ForegroundColor Green
Write-Host ('安装包：' + $setup.FullName)
Write-Host ('大小：' + [math]::Round($setup.Length / 1MB, 1) + ' MB    时间：' + $setup.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))

if ($OpenFolder)
{
    Start-Process explorer.exe ('/select,"' + $setup.FullName + '"')
}
