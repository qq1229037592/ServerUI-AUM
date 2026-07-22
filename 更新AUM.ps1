# v1.916: AUM 管理器自更新脚本 — 检测 GitHub 版本 → 下载源码 → 编译 → 替换
$ErrorActionPreference = "Continue"
$ScriptRoot = $PSScriptRoot

Write-Host ""
Write-Host "========================================"
Write-Host "  AUM 管理器自更新 v1.916"
Write-Host "========================================"
Write-Host ""

# ---- 查找 .NET SDK ----
Write-Host "[1/5] 查找 .NET 10 SDK..."
$dn = $null
try { $v = & dotnet --version 2>&1; if ($LASTEXITCODE -eq 0 -and $v -match '^\d+\.' -and [int]$Matches[1] -ge 10) { $dn = "dotnet" } } catch {}
if (-not $dn) { $pf = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"; if (Test-Path $pf) { $dn = $pf } }
if (-not $dn) { $l = Join-Path $ScriptRoot "dotnet-sdk\dotnet.exe"; if (Test-Path $l) { $dn = $l } }
if (-not $dn) { Write-Host "ERROR: 未找到 .NET 10 SDK!"; pause; exit 1 }
$ver = & $dn --version 2>&1
Write-Host "  SDK: v$($ver.Trim())"

# ---- 检测远程版本 ----
Write-Host ""
Write-Host "[2/5] 检测 GitHub 最新版本..."
$remoteVer = $null
$localVerFile = Join-Path $ScriptRoot "AUM-version.txt"
$localVer = if (Test-Path $localVerFile) { (Get-Content $localVerFile -Encoding UTF8).Trim() } else { "0" }
Write-Host "  本地版本: v$localVer"

# 尝试 Raw URL
try {
    $ts = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $resp = Invoke-WebRequest -Uri "https://raw.githubusercontent.com/118coder/ServerUI-AUM-S4A12/main/AUM-version.txt?t=$ts" -UseBasicParsing -TimeoutSec 10 -Headers @{ "User-Agent" = "ServerUI-AUM"; "Cache-Control" = "no-cache" }
    $remoteVer = $resp.Content.Trim() -replace '\s+', ''
    Write-Host "  远程版本: v$remoteVer (Raw)"
} catch {
    try {
        $apiUrl = "https://api.github.com/repos/118coder/ServerUI-AUM-S4A12/contents/AUM-version.txt?ref=main&t=$ts"
        $resp = Invoke-WebRequest -Uri $apiUrl -UseBasicParsing -TimeoutSec 10 -Headers @{ "User-Agent" = "ServerUI-AUM" }
        $json = $resp.Content | ConvertFrom-Json
        if ($json.content) {
            $bytes = [Convert]::FromBase64String(($json.content -replace '\s', ''))
            $remoteVer = [System.Text.Encoding]::UTF8.GetString($bytes).Trim()
            Write-Host "  远程版本: v$remoteVer (API)"
        }
    } catch {
        Write-Host "  ERROR: 无法连接 GitHub，请检查网络。"
        pause
        exit 1
    }
}

if (-not $remoteVer) { Write-Host "ERROR: 获取远程版本失败。"; pause; exit 1 }

# 版本比较
function Compare-Version($a, $b) {
    $pa = ($a -split '[.\-_]' | ForEach-Object { $_ -replace '[^0-9]','' } | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
    $pb = ($b -split '[.\-_]' | ForEach-Object { $_ -replace '[^0-9]','' } | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
    for ($i = 0; $i -lt [Math]::Max($pa.Count, $pb.Count); $i++) {
        $va = if ($i -lt $pa.Count) { $pa[$i] } else { 0 }
        $vb = if ($i -lt $pb.Count) { $pb[$i] } else { 0 }
        if ($va -ne $vb) { return $va.CompareTo($vb) }
    }
    return 0
}

$cmp = Compare-Version $remoteVer $localVer
if ($cmp -le 0) {
    Write-Host ""
    if ($cmp -lt 0) { Write-Host "当前版本 v$localVer 高于远程 v$remoteVer（开发版），无需更新。" }
    else { Write-Host "已是最新版本 v$localVer。" }
    pause
    exit 0
}

Write-Host ""
Write-Host "发现新版本 v$remoteVer！当前 v$localVer"
$confirm = Read-Host "是否进行更新? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") { Write-Host "已取消。"; pause; exit 0 }

# ---- 下载源码 ----
Write-Host ""
Write-Host "[3/5] 下载最新源码..."
$tmpDir = Join-Path $env:TEMP "AUM-selfupdate"
if (Test-Path $tmpDir) { Remove-Item -Recurse -Force $tmpDir }
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$tmpZip = Join-Path $tmpDir "source.zip"

$zipUrl = "https://api.github.com/repos/118coder/ServerUI-AUM-S4A12/zipball/main"
$ok = $false
for ($a = 1; $a -le 3; $a++) {
    try {
        Write-Host "  下载中 (第${a}次)..."
        Invoke-WebRequest -Uri $zipUrl -OutFile $tmpZip -UseBasicParsing -TimeoutSec 60 -Headers @{ "User-Agent" = "ServerUI-AUM" }
        if ((Test-Path $tmpZip) -and (Get-Item $tmpZip).Length -gt 10240) { $ok = $true; break }
    } catch { }
    if ($a -lt 3) { Start-Sleep -Seconds 3 }
}
if (-not $ok) { Write-Host "ERROR: 下载失败。"; pause; exit 1 }
Write-Host "  下载完成: $([math]::Round((Get-Item $tmpZip).Length/1KB)) KB"

# ---- 解压并编译 ----
Write-Host ""
Write-Host "[4/5] 编译新版本..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tmpExtract = Join-Path $tmpDir "extract"
if (Test-Path $tmpExtract) { Remove-Item -Recurse -Force $tmpExtract }
[System.IO.Compression.ZipFile]::ExtractToDirectory($tmpZip, $tmpExtract)

# 找 ServerUI 目录
$rootDir = $tmpExtract
$subDir = Get-ChildItem $tmpExtract -Directory | Where-Object { $_.Name -match "ServerUI|S4A12" } | Select-Object -First 1
if ($subDir) { $rootDir = $subDir.FullName }
$srcDir = Join-Path $rootDir "ServerUI"
if (-not (Test-Path $srcDir)) {
    $alt = Get-ChildItem $rootDir -Directory -Recurse -Filter "ServerUI" | Select-Object -First 1
    if ($alt) { $srcDir = $alt.FullName }
}
if (-not (Test-Path (Join-Path $srcDir "ServerUI.csproj"))) {
    Write-Host "ERROR: 源码结构异常。"
    pause
    exit 1
}

# 清理重复文件
foreach ($sub in @("Services", "Models")) {
    $subPath = Join-Path $srcDir $sub
    if (-not (Test-Path $subPath)) { continue }
    Get-ChildItem $subPath -File -Filter "*.cs" | ForEach-Object {
        $rootPath = Join-Path $srcDir $_.Name
        if (Test-Path $rootPath) { Remove-Item $rootPath -Force }
    }
}

# 编译框架依赖版
$pubDir = Join-Path $tmpDir "publish"
New-Item -ItemType Directory -Path $pubDir -Force | Out-Null
Write-Host "  编译中..."
& $dn restore (Join-Path $srcDir "ServerUI.csproj") --ignore-failed-sources 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: restore 失败。"; pause; exit 1 }
& $dn publish (Join-Path $srcDir "ServerUI.csproj") -c Release -r win-x64 --no-self-contained -o $pubDir 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    & $dn publish (Join-Path $srcDir "ServerUI.csproj") -c Release -r win-x64 --no-self-contained -o $pubDir 2>&1 | Out-Null
}
$newExe = Join-Path $pubDir "ServerUI.exe"
if (-not (Test-Path $newExe) -or (Get-Item $newExe).Length -lt 10240) {
    Write-Host "ERROR: 编译失败。"
    pause
    exit 1
}
$newSize = [math]::Round((Get-Item $newExe).Length / 1KB)
Write-Host "  编译成功: $newSize KB"

# 编译自包含版
$pubScDir = Join-Path $tmpDir "publish-sc"
New-Item -ItemType Directory -Path $pubScDir -Force | Out-Null
Write-Host "  编译自包含版..."
& $dn publish (Join-Path $srcDir "ServerUI.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false -o $pubScDir 2>&1 | Out-Null
$newScExe = Join-Path $pubScDir "ServerUI.exe"

# ---- 替换文件 ----
Write-Host ""
Write-Host "[5/5] 替换文件..."

# 停掉正在运行的 ServerUI
Get-Process -Name "ServerUI" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 替换 EXE
if (Test-Path $newScExe) {
    $scDest = Join-Path $ScriptRoot "开始游戏-ServerUI.exe"
    Copy-Item $newScExe $scDest -Force
    Write-Host "  开始游戏-ServerUI.exe: OK"
}
$fdDest = Join-Path $ScriptRoot "ServerUI-无依赖版.exe"
Copy-Item $newExe $fdDest -Force
Write-Host "  ServerUI-无依赖版.exe: OK"

# 同步源码到 AUM管理组件\ServerUI\
$localSrc = Join-Path $ScriptRoot "ServerUI"
if (-not (Test-Path $localSrc)) { New-Item -ItemType Directory -Path $localSrc -Force | Out-Null }
Get-ChildItem $srcDir -File -Recurse | ForEach-Object {
    $rel = $_.FullName.Substring($srcDir.Length).TrimStart('\')
    if ($rel -match '(^|\\)(bin|obj)(\\|$)') { return }
    $dst = Join-Path $localSrc $rel
    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    Copy-Item $_.FullName $dst -Force
}
Write-Host "  源码已同步"

# 同步仓库根文件到 AUM管理组件\
$exts = @("*.ps1", "*.bat", "*.txt", "*.md")
foreach ($pattern in $exts) {
    Get-ChildItem $rootDir -File -Filter $pattern | ForEach-Object {
        $name = $_.Name
        if ($name -match "GameLog|运行日志") { return }
        Copy-Item $_.FullName (Join-Path $ScriptRoot $name) -Force
    }
}

# 更新版本文件
Set-Content -Path $localVerFile -Value $remoteVer -Encoding UTF8
Write-Host "  AUM-version.txt → v$remoteVer"

# 清理
if (Test-Path $tmpDir) { Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue }

Write-Host ""
Write-Host "========================================"
Write-Host "  更新完成! 版本: v$localVer → v$remoteVer"
Write-Host "========================================"
Write-Host ""
pause
