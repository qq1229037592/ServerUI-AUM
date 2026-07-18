# ============================== ServerS4A12 一键更新脚本 ==============================
# 支持增量更新（默认，只更新最近 3 天变更的文件）和全量同步（-FullSync 开关）
# 全流程：[1/5] 备份数据库 → [2/5] 下载最新源码 → [3/5] 更新文件 → [4/5] 编译 → [5/5] 提交日志
param([switch]$FullSync, [switch]$NonInteractive)    # -FullSync：全量同步开关，不加此参数默认执行增量更新；-NonInteractive：非交互模式（从GUI调用时使用，跳过人工确认）

$ErrorActionPreference = "Stop"       # 关键下载、解压和编译步骤失败时必须终止，避免误报更新成功
$ScriptRoot = $PSScriptRoot; $SrcRoot = Join-Path $ScriptRoot "ServerS4A12-AUM"    # ScriptRoot=脚本所在目录，SrcRoot=服务器主目录
$RepoApi = "https://codeberg.org/api/v1/repos/rewio/ServerS4A12"    # Codeberg 仓库 API 地址
$utf8 = [System.Text.Encoding]::UTF8

# base64 编码的中文字符串字典，避免汉字在控制台/日志中出现乱码
$b64 = @{
    fn_log  = "5pu05paw5pel5b+XLnR4dA=="
    s_ver   = "54mI5pysOiA="
    s_up    = "5pu05paw5pe26Ze0OiA="
    s_total = "57Sv6K6h5o+Q5LqkOiA="
    s_hist  = "5pu05paw5Y6G5Y+yICjku47mnIDliJ3liLDnjrDlnKjvvIzljJfkuqzml7bpl7QgVVRDKzgpOg=="
    s_more  = "5pu05aSa5Y6G5Y+y5pu05paw5pel5b+X77yM6K+35Zyo55uu5b2V5p+l55yLOiA="
    s_repo  = "5LuT5bqT5o+Q5Lqk6K6w5b2VOiA="
    s_inc   = "5aKe6YeP5pu05paw"
    s_full  = "5YWo6YeP5ZCM5q2l"
    s_fullsync = "5YWo6YeP5ZCM5q2lICjmiYDmnInmlofku7YpLi4u"
    s_fallback = "5pyA6L+R5peg5Y+Y5pu0IC0g5Zue6YCA5Yiw5YWo6YeP5ZCM5q2l44CC"
    s_server = "U2VydmVyUzRBMTIgLSA="
    s_updating = "Pj4+IFszLzVdIOato+WcqOabtOaWsOaWh+S7tiAo"
    s_done  = "Pj4+IOWujOaIkCEg"
    s_warn1 = "5byA5aeL5pu05paw5YmN77yM6K+356Gu6K6k572R57uc54iL6YCa77yM5Lim5qOA5p+l5piv5ZCm5bey5byA5ZCv56eR5a2m5LiK572R44CC"
    s_warn2 = "5pys5Zyw5paH5Lu26Lev5b6E5Y+C6ICD77ya"
    s_warn3 = "ICAtIOaVsOaNruW6k+WtmOahozogXFNlcnZlclM0QTEyLUFVTVxkaXN0XHdpbi14NjRcRGF0YVxpbnZlbnRvcnkuZGI="
    s_warn4 = "ICAtIFBWRuaWh+S7tjogICBcU2VydmVyUzRBMTItQVVNXGRpc3Rcd2luLXg2NFxEYXRhXFB2ZlxTY3JpcHQucHZm"
    s_warn5 = "6K+356Gu6K6k5LiK6L+w5paH5Lu25L2N572u5peg6K+v44CC"
    s_skip   = "ICBb5L+d5oqkXSA="
    s_prot   = "ICAo5bey5L+d5oqk77yM5LiN5Lya6KKr6KaG55GWKQ=="
}
function T($key) { return $utf8.GetString([Convert]::FromBase64String($b64[$key])) }    # 将 base64 字符串解码为中文文本

$LogFile   = Join-Path $ScriptRoot (T "fn_log")    # 更新日志文件路径（脚本同目录下）
$DbFile = Join-Path $SrcRoot "Server\DfoServer\Data\inventory.db"    # 玩家数据数据库文件（最重要！）
$DbBackup = Join-Path $SrcRoot "Server\DfoServer\Data\inventory.db.bak"    # 数据库临时备份文件
$TempDir   = Join-Path $env:TEMP "ServerS4A12-update"    # 下载和解压源码用的临时目录
$LocalSdk  = Join-Path $ScriptRoot "dotnet-sdk"    # 本地 .NET SDK 安装目录（脚本同目录下）
$CommitCacheFile = Join-Path $ScriptRoot ".update-cache\commits.json"
$ChinaTZ   = [System.TimeZoneInfo]::FindSystemTimeZoneById("China Standard Time")    # 中国标准时间（UTC+8）时区对象

function ToChinaDate($d) {    # 将 UTC 时间字符串转换为北京时间（UTC+8）的日期格式 yyyy-MM-dd
    $dt = [DateTimeOffset]::Parse($d, [System.Globalization.CultureInfo]::InvariantCulture)
    return ([System.TimeZoneInfo]::ConvertTime($dt, $ChinaTZ)).ToString("yyyy-MM-dd")
}

function Download-File($uri, $target) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Write-Host "Download attempt $attempt/5: $uri"
            Invoke-WebRequest -Uri $uri -OutFile $target -UseBasicParsing -TimeoutSec 60
            if ((Test-Path $target) -and (Get-Item $target).Length -gt 1024) { return $true }
            throw "Downloaded file is missing or too small."
        } catch {
            Remove-Item $target -Force -ErrorAction SilentlyContinue
            Write-Host "WARNING: download attempt $attempt failed: $_"
            if ($attempt -lt 5) { Start-Sleep -Seconds ([math]::Pow(2, $attempt - 1)) }
        }
    }
    return $false
}

function Invoke-RepositoryRequest($uri, [int]$MaxAttempts = 2, [int]$TimeoutSec = 8, [switch]$Quiet) {
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            if (-not $Quiet) { Write-Host "[API] Request $attempt/$MaxAttempts" }
            return Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec $TimeoutSec
        } catch {
            Write-Host "WARNING: API attempt $attempt/$MaxAttempts failed: $_"
            if ($attempt -lt $MaxAttempts) {
                $delay = [math]::Pow(2, $attempt - 1)
                Write-Host "[API] Retrying in $delay second(s)..."
                Start-Sleep -Seconds $delay
            }
        }
    }
    Write-Host "WARNING: API unavailable after $MaxAttempts attempts. Falling back to archive-only update."
    return $null
}

function Read-CommitCache {
    if (-not (Test-Path $CommitCacheFile)) { return @() }
    try {
        $data = Get-Content $CommitCacheFile -Raw -Encoding UTF8 | ConvertFrom-Json
        return @($data)
    } catch {
        Write-Host "WARNING: commit cache is invalid and will be rebuilt."
        return @()
    }
}

function Write-CommitCache($commits) {
    $directory = Split-Path $CommitCacheFile -Parent
    if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $commits | ConvertTo-Json -Depth 4 | Set-Content $CommitCacheFile -Encoding UTF8
}

function Sync-CommitHistory {
    $cached = Read-CommitCache
    $known = @{}
    foreach ($commit in $cached) { if ($commit.Sha) { $known[$commit.Sha] = $commit } }
    $wasComplete = $cached.Count -gt 0
    $page = 1; $perPage = 100; $reachedHistoryEnd = $false; $hitCacheBoundary = $false
    $newCount = 0

    while ($true) {
        $response = Invoke-RepositoryRequest "$RepoApi/commits?sha=main&limit=$perPage&page=$page" -Quiet
        if (-not $response) {
            Write-Host "[提交日志] 网络不稳定，保留上次完整缓存，不影响本次更新。"
            return @{ Commits = $cached; Complete = $wasComplete; Refreshed = $false }
        }
        try { $items = @($utf8.GetString($response.RawContentStream.ToArray()) | ConvertFrom-Json) }
        catch {
            Write-Host "[提交日志] API 返回无法解析，保留上次完整缓存。"
            return @{ Commits = $cached; Complete = $wasComplete; Refreshed = $false }
        }
        if ($items.Count -eq 0) { $reachedHistoryEnd = $true; break }

        foreach ($item in $items) {
            $sha = $item.sha
            if ($known.ContainsKey($sha)) { $hitCacheBoundary = $true; break }
            $known[$sha] = [pscustomobject]@{
                Sha = $sha
                Date = $item.commit.committer.date
                Message = $item.commit.message
            }
            $newCount++
        }
        if ($hitCacheBoundary -or $items.Count -lt $perPage) {
            if ($items.Count -lt $perPage) { $reachedHistoryEnd = $true }
            break
        }
        $page++
        Write-Host "[提交日志] 正在建立完整缓存: 已读取 $($known.Count) 条提交..."
    }

    $merged = @($known.Values | Sort-Object { [DateTimeOffset]$_.Date } -Descending)
    if ($reachedHistoryEnd -or $wasComplete) {
        Write-CommitCache $merged
        Write-Host "[提交日志] 已同步 $newCount 条新提交，缓存共 $($merged.Count) 条。"
        return @{ Commits = $merged; Complete = $true; Refreshed = $true }
    }

    Write-Host "[提交日志] 历史缓存尚未完整，本次保留已有日志，后续更新会自动续传。"
    return @{ Commits = $cached; Complete = $false; Refreshed = $false }
}

function Test-ZipFile($path) {
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
        $valid = $zip.Entries.Count -gt 0
        $zip.Dispose()
        return $valid
    } catch { return $false }
}

function Sync-SourceFiles($from, $to) {
    $changes = 0
    Get-ChildItem $from -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($from.Length).TrimStart('\')
        if ($relative -match '(^|\\)(\.git|dist)(\\|$)') { return }
        if ($relative -match '(^|\\)inventory\.db(\.bak)?$') { return }
        if ($relative -match '(^|\\)start-server\.(bat|sh)$') { return }
        $destination = Join-Path $to $relative
        $existing = Get-Item $destination -ErrorAction SilentlyContinue
        # Level 1: size and timestamp. Matching metadata can skip hashing entirely.
        if ($existing -and $existing.Length -eq $_.Length -and $existing.LastWriteTimeUtc -eq $_.LastWriteTimeUtc) { return }

        # Level 2: metadata differs, so compare SHA-256 and avoid copying identical content.
        $sameContent = $false
        if ($existing) {
            $sourceHash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
            $destinationHash = (Get-FileHash $destination -Algorithm SHA256).Hash
            $sameContent = $sourceHash -eq $destinationHash
        }
        if ($sameContent) { return }

        # Level 3: content differs. Retry write and verify the copied SHA-256 each time.
        $updated = $false
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                $directory = Split-Path $destination -Parent
                if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
                Copy-Item $_.FullName $destination -Force
                $sourceHash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
                $destinationHash = (Get-FileHash $destination -Algorithm SHA256).Hash
                if ($sourceHash -ne $destinationHash) { throw "SHA-256 verification failed." }
                [System.IO.File]::SetLastWriteTimeUtc($destination, $_.LastWriteTimeUtc)
                $updated = $true
                break
            } catch {
                Write-Host "WARNING: file sync $attempt/3 failed for ${relative}: $_"
                if ($attempt -lt 3) { Start-Sleep -Seconds ([math]::Pow(2, $attempt - 1)) }
            }
        }
        if ($updated) {
            $action = if ($existing) { "更新" } else { "下载" }
            Write-Host "[FILE] $action $relative | 更新日期 $($_.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
            $changes++
        } else {
            Write-Host "ERROR: skipped $relative after 3 failed write attempts."
        }
    }
    return $changes
}

function Remove-StaleSourceFiles($from, $to) {
    $removed = 0
    # Only remove stale C# source files from repository-managed code directories.
    # Runtime data, inventory.db, launch scripts, and user files are never considered here.
    foreach ($folder in @("Server", "Tool")) {
        $localFolder = Join-Path $to $folder
        if (-not (Test-Path $localFolder)) { continue }
        Get-ChildItem $localFolder -File -Recurse -Filter "*.cs" | ForEach-Object {
            $relative = $_.FullName.Substring($to.Length).TrimStart('\')
            if ($relative -match '(^|\\)(bin|obj)(\\|$)') { return }
            $sourcePath = Join-Path $from $relative
            if (-not (Test-Path $sourcePath)) {
                Remove-Item $_.FullName -Force
                Write-Host "[FILE] 删除过期源码 $relative"
                $removed++
            }
        }
    }
    return $removed
}

function Get-DotNetExe {    # 检测 .NET SDK 是否存在，按优先级：系统全局 > Program Files > 本地 dotnet-sdk 目录
    $sys = "dotnet"
    try { $v = & dotnet --version 2>&1; if ($LASTEXITCODE -eq 0 -and $v -match "^(\d+)\.(\d+)" -and [int]$matches[1] -ge 10) { return $sys } } catch { }
    try { $pf = "$env:ProgramFiles\dotnet\dotnet.exe"; if (Test-Path $pf) { $v = & $pf --version 2>&1; if ($LASTEXITCODE -eq 0 -and $v -match "^(\d+)\.(\d+)" -and [int]$matches[1] -ge 10) { return $pf } } } catch { }
    $local = Join-Path $LocalSdk "dotnet.exe"
    if (Test-Path $local) { return $local }
    return $null
}

function Ensure-DotNet10 {    # 确保 .NET 10 SDK 可用：优先检测系统现有 SDK，没有则自动下载安装到本地 dotnet-sdk 目录
    $dn = Get-DotNetExe
    if ($dn) {
        $ver = & $dn --version 2>&1
        $verStr = $ver.Trim()
        if ($dn -eq "dotnet" -or $dn -like "*Program Files*") {
            Write-Host "  Using system .NET SDK ($verStr)"
        } else {
            Write-Host "  Using bundled .NET SDK ($verStr)"
        }
        return $dn
    }
    Write-Host "  .NET 10 SDK not found."
    Write-Host "  .NET 10 SDK is required. Click 安装SDK in ServerUI, finish the Microsoft installer, then restart ServerUI."
    return $null
}

try {    # ===== 主更新流程开始：共 5 个步骤 =====
    Set-Location $SrcRoot
    $currentDate  = Get-Date -Format "yyyy-MM-dd"
    $currentTime  = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $threeDaysAgo = (Get-Date).AddHours(-72)

    $modeText = if ($FullSync) { T "s_full" } else { T "s_inc" }
    Write-Host ""
    Write-Host "========================================"
    Write-Host "  $(T 's_server')$modeText"
    Write-Host "  Date: $currentDate (UTC+8)"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "[ $(T 's_warn1') ]"
    Write-Host "$(T 's_warn2')"
    Write-Host "$(T 's_warn3')"
    Write-Host "$(T 's_warn4')"
    Write-Host "$(T 's_warn5')"
    Write-Host ""
    Write-Host "========================================"
    Write-Host ""

    if (Test-Path $LogFile) {
        $raw = [System.IO.File]::ReadAllText($LogFile, $utf8)
        $rx = [regex]::Matches($raw, "\d{4}-\d{2}-\d{2}")
        if ($rx.Count -gt 0) {
            $lv = $rx[$rx.Count - 1].Value
            if ($lv -eq $currentDate) { Write-Host "Last version: $lv (up-to-date)" }
            else { Write-Host "Last version: $lv ($(((Get-Date $currentDate)-(Get-Date $lv)).Days)d ago)" }
        }
    } else { Write-Host "First run." }

    Write-Host ""
    Write-Host ">>> [1/5] Backing up inventory.db <<<"    # [1/5] 先备份玩家数据库，防止更新过程中数据丢失
    $dbExisted = Test-Path $DbFile
    if ($dbExisted) { Copy-Item $DbFile $DbBackup -Force; Write-Host "OK ($((Get-Item $DbFile).Length) bytes)" }
    else { Write-Host "No inventory.db, skip." }

    Write-Host ""
    Write-Host ">>> [2/5] Downloading source <<<"    # [2/5] 从 Codeberg 仓库下载最新主分支 ZIP 源码包
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    $TempZip = Join-Path $TempDir "main.zip"
    $TempExtract = Join-Path $TempDir "extract"
    $ProgressPreference = "SilentlyContinue"
    if (-not (Download-File "https://codeberg.org/rewio/ServerS4A12/archive/main.zip" $TempZip) -or -not (Test-ZipFile $TempZip)) {
        Write-Host "ERROR: Download failed."
        if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }
        exit 1
    }
    Write-Host "OK ($([math]::Round((Get-Item $TempZip).Length/1KB)) KB)"

    Write-Host ""
    Write-Host "$(T 's_updating')$modeText) <<<"    # [3/5] 更新文件：增量模式只更新最近变更的文件，全量模式同步所有历史文件；同时保护 inventory.db 和 start-server.bat 不被覆盖
    try { Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force }
    catch { Write-Host "ERROR: Extraction failed: $_"; if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }; exit 1 }
    $srcDir = Get-ChildItem -Path $TempExtract -Directory | Select-Object -First 1
    if (-not $srcDir) {
        Write-Host "ERROR: Extraction failed."
        if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }
        exit 1
    }
    $srcPath = $srcDir.FullName

    # The archive is the source of truth. Avoid slow compare/commit API calls here;
    # Sync-SourceFiles performs the three-level file verification below.
    if ($FullSync) { Write-Host (T "s_fullsync") }
    else { Write-Host "Incremental mode: archive sync will update only content that changed." }

    # Final sync is the authoritative source update. It logs each changed file and prevents API omissions.
    Write-Host "Safety sync: checking every source file..."
    $safetyChanges = Sync-SourceFiles $srcPath $SrcRoot
    $staleRemoved = Remove-StaleSourceFiles $srcPath $SrcRoot
    Write-Host "Safety check done: $safetyChanges file(s) downloaded or updated, $staleRemoved stale source file(s) removed."

    if ($dbExisted) {
        Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force
        Write-Host "inventory.db restored."
    }
    Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host ">>> [4/5] Building <<<"    # [4/5] 编译：使用 dotnet publish 将 C# 源码编译为单个可执行文件 DfoServer.exe，发布到 dist 目录
    $dn = Ensure-DotNet10
    $buildOk = $false

    $distDb = Join-Path $SrcRoot "dist\win-x64\Data\inventory.db"
    $distDbBak = Join-Path $SrcRoot "dist\win-x64\Data\inventory.db.tmpbak"

    if (Test-Path $distDb) {
        Copy-Item $distDb $distDbBak -Force
    }

    if ($dn) {
        $serverProject = Join-Path $SrcRoot "Server\DfoServer\DfoServer.csproj"
        $distDir = Join-Path $SrcRoot "dist\win-x64"
        $serverDir = Split-Path $serverProject -Parent
        if (-not (Test-Path $serverProject)) { throw "Server project not found: $serverProject" }
        Write-Host "Compiling server with .NET SDK: $(& $dn --version)"
        Push-Location $serverDir
        try {
            & $dn restore $serverProject --ignore-failed-sources 2>&1
            if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed (exit $LASTEXITCODE)." }
            & $dn publish $serverProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $distDir 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "WARNING: first server publish failed; clearing obj and retrying once..."
                Remove-Item (Join-Path $serverDir "obj") -Recurse -Force -ErrorAction SilentlyContinue
                & $dn publish $serverProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $distDir 2>&1
            }
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }
            $exe = Get-Item (Join-Path $distDir "DfoServer.exe") -ErrorAction SilentlyContinue
            if (-not $exe -or $exe.Length -le 0) { throw "DfoServer.exe was not produced." }
            Write-Host "OK - DfoServer.exe ($([math]::Round($exe.Length/1MB,2)) MB)"
            $buildOk = $true
        } catch {
            Write-Host "ERROR: Server compilation failed: $_"
        } finally { Pop-Location }
    } else { Write-Host "Could not obtain .NET SDK. Skipping build." }

    if (Test-Path $distDbBak) {
        Copy-Item $distDbBak $distDb -Force
        Remove-Item $distDbBak -Force
        Write-Host "Restored dist inventory.db"
    }

    $checkFiles = @(    # 编译后补充检查：确保 SQL 模式文件和配置文件也被复制到 dist 发布目录
        @{src="Server\DfoServer\Sqlite\item_schema.sql"; dst="dist\win-x64\Sqlite\item_schema.sql"},
        @{src="Server\DfoServer\channel_info.etc"; dst="dist\win-x64\channel_info.etc"}
    )
    foreach ($cf in $checkFiles) {
        $dp = Join-Path $SrcRoot $cf.dst
        if (-not (Test-Path $dp)) {
            $sp = Join-Path $SrcRoot $cf.src
            if (Test-Path $sp) {
                $dd = Split-Path $dp -Parent
                if (-not (Test-Path $dd)) { New-Item -ItemType Directory -Path $dd -Force | Out-Null }
                Copy-Item $sp $dp -Force
                Write-Host "Fixed: copied $($cf.dst) from source"
            }
        }
    }

    Write-Host ""
    Write-Host ">>> GM Tool Build <<<"
    $gmRepo = "https://codeberg.org/rewio/DfoGmTool"
    $gmDir = Join-Path $ScriptRoot "dfogmtool"
    $gmExtract = Join-Path $env:TEMP "ServerS4A12-gmtool"
    $gmBuildOk = $false

    if ($dn) {
        try {
            # 停止正在运行的GM工具进程（否则 publish 目录中 DLL 被锁定，编译会失败）
            $gmExePath = Join-Path $gmDir "publish\DfoGmTool.exe"
            if (Test-Path $gmExePath) {
                try { Get-Process -Name "DfoGmTool" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep 1 } catch { }
                Write-Host "Stopped existing GM tool process."
            }

            $gmProject = Join-Path $gmDir "DfoGmTool.csproj"
            if (-not (Test-Path $gmProject)) {
                Write-Host "GM tool source missing; downloading once..."
                if (Test-Path $gmExtract) { Remove-Item -Recurse -Force $gmExtract }
                New-Item -ItemType Directory -Path $gmExtract -Force | Out-Null
                $gmZip = Join-Path $gmExtract "main.zip"; $gmSrcDir = Join-Path $gmExtract "extract"
                if (-not (Download-File "$gmRepo/archive/main.zip" $gmZip) -or -not (Test-ZipFile $gmZip)) { throw "GM tool source download failed." }
                Expand-Archive -Path $gmZip -DestinationPath $gmSrcDir -Force
                $gmSrc = Get-ChildItem -Path $gmSrcDir -Directory | Select-Object -First 1
                if (-not $gmSrc) { throw "GM tool source extraction failed." }
                if (-not (Test-Path $gmDir)) { New-Item -ItemType Directory -Path $gmDir -Force | Out-Null }
                Copy-Item "$($gmSrc.FullName)\*" $gmDir -Recurse -Force
                $gmProject = Join-Path $gmDir "DfoGmTool.csproj"
            }
            if (-not (Test-Path $gmProject)) { throw "GM tool project not found." }
            Write-Host "Compiling existing local GM tool source (download skipped)..."
            & $dn publish $gmProject -c Release -r win-x64 --self-contained true -o "$gmDir\publish" 2>&1
            if ($LASTEXITCODE -ne 0) { throw "GM tool publish failed (exit $LASTEXITCODE)." }
            $gmExe = Get-Item "$gmDir\publish\DfoGmTool.exe" -ErrorAction SilentlyContinue
            if (-not $gmExe) { throw "DfoGmTool.exe was not produced." }
            Write-Host "OK - DfoGmTool.exe ($([math]::Round($gmExe.Length/1MB,2)) MB)"
            $gmBuildOk = $true
        } catch {
            Write-Host "WARNING: GM tool update failed: $_"
        }

        if (Test-Path $gmExtract) { Remove-Item -Recurse -Force $gmExtract -ErrorAction SilentlyContinue }
    } else { Write-Host "No .NET SDK, skipping GM tool build." }

    Write-Host ""
    Write-Host ">>> [5/5] Commit log cache <<<"
    $allGrouped = @{}
    $history = Sync-CommitHistory
    $preserveExistingLog = @($history.Commits).Count -eq 0 -and (Test-Path $LogFile)
    foreach ($c in @($history.Commits)) {
        try {
            $d = ToChinaDate $c.Date
            # Keep the complete commit message, including any body text, rather than truncating it.
            $message = ($c.Message -replace "`r?`n", "`n").Trim()
            if (-not $allGrouped.Contains($d)) { $allGrouped[$d] = @() }
            $allGrouped[$d] += $message
        } catch { }
    }

    $sortedDates = $allGrouped.Keys | Sort-Object -Descending    # 按日期降序排列（新日期在上，旧日期在下）——用于写入日志文件
    $sortedDatesAsc = $allGrouped.Keys | Sort-Object    # 按日期升序排列（旧日期在上，新日期在下）——用于控制台输出
    $totalCommits = 0; foreach ($d in $sortedDates) { $totalCommits += $allGrouped[$d].Count }

    Write-Host ""
    Write-Host "========================================"
    Write-Host "$(T 's_done')$modeText"
    Write-Host "  Version: $currentDate | Commits: $totalCommits"
    if ($buildOk) { Write-Host "  Server Build: OK" } else { Write-Host "  Server Build: FAILED" }
    if ($gmBuildOk) { Write-Host "  GM Tool Build: OK" } else { Write-Host "  GM Tool Build: Skipped" }
    Write-Host "========================================"
    Write-Host ""

    $ver = T "s_ver"; $up = T "s_up"; $total = T "s_total"; $hist = T "s_hist"
    $logLines = [System.Collections.ArrayList]::new()
    [void]$logLines.Add("========================================")
    [void]$logLines.Add($ver + $currentDate)
    [void]$logLines.Add($up + $currentTime)
    [void]$logLines.Add($total + $totalCommits)
    [void]$logLines.Add("方式: " + $modeText)
    $bs = if ($buildOk) { "OK" } else { "Failed" }
    [void]$logLines.Add("Server Build: " + $bs)
    $gbs = if ($gmBuildOk) { "OK" } else { "Skipped" }
    [void]$logLines.Add("GM Tool Build: " + $gbs)
    [void]$logLines.Add("========================================")
    [void]$logLines.Add(""); [void]$logLines.Add($hist); [void]$logLines.Add("")
    foreach ($d in $sortedDates) {
        [void]$logLines.Add("--- $d ($($allGrouped[$d].Count) commits) ---")
        foreach ($m in $allGrouped[$d]) {
            foreach ($line in ($m -split "`n")) { [void]$logLines.Add("  $line") }
        }
        [void]$logLines.Add("")
    }
    [void]$logLines.Add("========================================")
    [void]$logLines.Add("")

    $logText = ($logLines -join "`r`n") + "`r`n"
    if ($preserveExistingLog) {
        Write-Host "[提交日志] 本次无法取得历史缓存，保留现有 更新日志.txt，不覆盖完整提交记录。"
    } else {
        [System.IO.File]::WriteAllText($LogFile, $logText, (New-Object System.Text.UTF8Encoding $true))
    }

    $sda = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd")
    foreach ($d in $sortedDatesAsc) {    # 控制台输出按日期升序显示（旧日期在上，新日期在下），仅显示最近 7 天的 commit
        if ($d -lt $sda) { continue }
        Write-Host "--- $d ($($allGrouped[$d].Count) commits) ---"
        foreach ($m in $allGrouped[$d]) {
            foreach ($line in ($m -split "`n")) { Write-Host "  $line" }
        }
        Write-Host ""
    }
    if (($sortedDatesAsc | Where-Object { $_ -lt $sda })) {
        Write-Host "---"
        Write-Host ((T "s_more") + (T "fn_log"))
        Write-Host ((T "s_repo") + "https://codeberg.org/rewio/ServerS4A12/commits/branch/main")
    }
    if (-not $buildOk) {
        Write-Host "ERROR: Update files were synchronized but the server build did not succeed."
        exit 1
    }

} catch {    # 出错时恢复：尝试还原数据库备份，清理临时目录，然后退出
    Write-Host "ERROR: $_"
    if (Test-Path $DbBackup) { Copy-Item $DbBackup $DbFile -Force -ErrorAction SilentlyContinue; Remove-Item $DbBackup -Force }
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue }
    exit 1
}
