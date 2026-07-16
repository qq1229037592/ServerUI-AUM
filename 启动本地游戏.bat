@echo off
chcp 936 >nul
setlocal EnableDelayedExpansion

rem ============================================================
rem  启动本地游戏.bat — DNF 一键启动脚本
rem ============================================================
rem
rem  功能: 自动查找并启动服务端+客户端，游戏退出后自动清理
rem  运行: 双击此文件，窗口会自动最小化到任务栏
rem
rem  工作流程:
rem    1. 扫描目录查找服务端和客户端文件
rem    2. 清理端口 7001 残留进程
rem    3. 启动服务端 (隐藏窗口)
rem    4. 等待服务端就绪 (端口 7001 可访问)
rem    5. 启动游戏客户端 (DNF.exe 自动最小化)
rem    6. 监控 DNF.exe，退出后自动清理服务端
rem
rem  存放位置: 客户端与服务端目录的同级或子目录下均可
rem  支持结构: 脚本会自动递归搜索子目录
rem ============================================================

rem ----- 自启动到最小化窗口 -----
rem 原理: 首次启动时带上 _go 参数重新调用自身，以最小化方式运行
if "%1"=="_go" goto main
start /min "" cmd.exe /c "%~f0" _go
exit /b

:main
title DNF — 本地游戏启动器
set "BASE=%~dp0"

echo ========================================
echo   DNF 本地游戏 — 一键启动脚本
echo ========================================
echo.
echo 正在扫描目录，查找服务端与客户端...

rem ----- 1. 查找 DfoServer.exe (服务端主程序) -----
echo [检测] 查找 DfoServer.exe...
set "SRV_EXE="
for /f "delims=" %%i in ('dir /s /b "%BASE%DfoServer.exe" 2^>nul') do set "SRV_EXE=%%i"
if not defined SRV_EXE (
    echo [错误] 未找到 DfoServer.exe
    echo   请确保 AUM管理组件\ServerS4A12-AUM\dist\win-x64\ 目录下有 DfoServer.exe
    echo   如果不存在，请先在 ServerUI 中执行一次更新。
    pause
    exit /b
)
echo   OK: !SRV_EXE!

rem 根据 DfoServer.exe 位置反推服务端目录(向上两级 = ServerS4A12-AUM 目录)
for %%i in ("!SRV_EXE!") do set "SRV_DIST_DIR=%%~dpi"
for %%i in ("!SRV_DIST_DIR!..\..") do set "SRV_DIR=%%~fi\"
echo   服务端目录: !SRV_DIR!

rem 查找 start-server.bat
set "SRV_BAT="
if exist "!SRV_DIR!start-server.bat" (
    set "SRV_BAT=!SRV_DIR!start-server.bat"
) else (
    for /f "delims=" %%i in ('dir /s /b "!SRV_DIR!start-server.bat" 2^>nul') do set "SRV_BAT=%%i"
)
if not defined SRV_BAT (
    echo [错误] 未找到 start-server.bat
    pause
    exit /b
)
echo   启动脚本: !SRV_BAT!

rem ----- 2. 查找 DNF.exe (客户端主程序) -----
echo.
echo [检测] 查找 DNF.exe...
set "CLI_EXE="

rem 优先在脚本同级目录查找
for /f "delims=" %%i in ('dir /s /b "%BASE%DNF.exe" 2^>nul') do set "CLI_EXE=%%i"
if defined CLI_EXE goto found_cli

rem 在上级目录查找
for /f "delims=" %%i in ('dir /s /b "%BASE%..\DNF.exe" 2^>nul') do set "CLI_EXE=%%i"
if defined CLI_EXE goto found_cli

rem 继续向上查找
for /f "delims=" %%i in ('dir /s /b "%BASE%..\..\DNF.exe" 2^>nul') do set "CLI_EXE=%%i"
if defined CLI_EXE goto found_cli

echo [错误] 未找到 DNF.exe
echo   请在脚本所在目录或上级目录放置 DNF.exe 游戏客户端。
pause
exit /b

:found_cli
echo   OK: !CLI_EXE!
for %%i in ("!CLI_EXE!") do set "CLI_DIR=%%~dpi"

rem 查找客户端启动脚本 (本地游戏S4.bat / 单机游戏启动.bat)
set "CLI_BAT="
if exist "!CLI_DIR!本地游戏S4.bat" (
    set "CLI_BAT=!CLI_DIR!本地游戏S4.bat"
) else if exist "!CLI_DIR!单机游戏启动.bat" (
    set "CLI_BAT=!CLI_DIR!单机游戏启动.bat"
) else (
    rem 在客户端目录搜索任意 .bat 启动脚本 (排除自身)
    for /f "delims=" %%i in ('dir /b "!CLI_DIR!*.bat" 2^>nul ^| findstr /v /i "启动本地游戏 停止服务"') do (
        set "CLI_BAT=!CLI_DIR!%%i"
        goto found_bat
    )
)
:found_bat
if not defined CLI_BAT (
    echo [警告] 未找到客户端启动脚本 (.bat文件)
    echo   将直接启动 DNF.exe
    set "CLI_BAT="
) else (
    echo   客户端脚本: !CLI_BAT!
)

rem ----- 3. 清理端口 7001 残留进程 -----
echo.
echo [1/3] 正在清理残留服务端进程...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":7001 .*LISTENING" 2^>nul') do (
    >nul 2>&1 taskkill /f /pid %%a
    timeout /t 1 /nobreak >nul
)
echo   清理完成。

rem ----- 4. 启动服务端 (隐藏窗口) -----
echo.
echo [2/3] 正在启动服务端...
rem 使用 VBScript 以隐藏窗口方式启动服务端脚本
>"%temp%\dnf_srv.vbs" echo Set ws=CreateObject("WScript.Shell"^):ws.Run """!SRV_BAT!""",0,False
cscript //nologo "%temp%\dnf_srv.vbs"
del "%temp%\dnf_srv.vbs"

echo   等待服务端就绪 (检查端口 7001)...
rem 最多等待 30 秒
set /a COUNT=0
:wait_srv
timeout /t 2 /nobreak >nul
netstat -an | findstr ":7001 .*LISTENING" >nul 2>&1
if not errorlevel 1 goto srv_ready
set /a COUNT+=2
if !COUNT! lss 30 goto wait_srv
echo [警告] 等待超时 (30秒)，端口 7001 未就绪。
echo   服务端可能启动失败，请检查 AUM管理组件\ServerS4A12-AUM 目录。
echo   将尝试继续启动客户端...
:srv_ready
echo   服务端已就绪 (等待 !COUNT! 秒)
timeout /t 3 /nobreak >nul

rem ----- 5. 启动游戏客户端 (最小化窗口) -----
echo.
echo [3/3] 正在启动客户端...
if defined CLI_BAT (
    rem 通过 VBScript 启动客户端脚本 (最小化窗口)
    >"%temp%\dnf_cli.vbs" echo Set ws=CreateObject("WScript.Shell"^):ws.Run "cmd.exe /c cd /d ""!CLI_DIR!"" && ""!CLI_BAT!""",0,False
    cscript //nologo "%temp%\dnf_cli.vbs"
    del "%temp%\dnf_cli.vbs"
) else (
    rem 直接启动 DNF.exe (最小化)
    >"%temp%\dnf_cli.vbs" echo Set ws=CreateObject("WScript.Shell"^):ws.Run """!CLI_EXE!""",0,False
    cscript //nologo "%temp%\dnf_cli.vbs"
    del "%temp%\dnf_cli.vbs"
)

echo.
echo ========================================
echo   游戏已启动！
echo   窗口已自动最小化到任务栏。
echo   游戏退出后将自动关闭服务端。
echo ========================================
echo.

rem ----- 6. 监控 DNF.exe 进程，退出后自动清理 -----
:wait_exit
timeout /t 2 /nobreak >nul
rem 检查 DNF.exe 是否还在运行
tasklist /fi "imagename eq DNF.exe" 2>nul | findstr /i "DNF.exe" >nul 2>&1
if errorlevel 1 (
    rem DNF.exe 不在运行 → 游戏已退出
    goto cleanup
)
rem 检查 start-server.bat 窗口是否还在 (可能在游戏中就被关了)
tasklist /fi "imagename eq cmd.exe" 2>nul | findstr /i "cmd.exe" >nul 2>&1
goto wait_exit

:cleanup
echo.
echo 游戏已退出，正在清理服务端进程...

rem 杀端口 7001
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":7001 " 2^>nul') do (
    >nul 2>&1 taskkill /f /pid %%a
)

rem 杀所有 DfoServer.exe
taskkill /f /im DfoServer.exe >nul 2>&1

rem 1.5 秒后自动关闭
echo 清理完成，窗口即将关闭...
timeout /t 3 /nobreak >nul
exit
