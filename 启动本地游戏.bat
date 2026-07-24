@echo off
chcp 936 >nul
setlocal EnableDelayedExpansion

if "%1"=="_go" goto main
start /min "" cmd.exe /c "%~f0" _go
exit /b

:main
title DNF ������Ϸ - �����ű�
set "BASE=%~dp0"

rem ���浱ǰ cmd.exe �� PID����̨�����?
powershell -NoProfile -ExecutionPolicy Bypass -File "%BASE%ps1����\get_pid.ps1"

rem ��̨���? ���ڹر�ʱ�رշ����?
start /b "" powershell -NoProfile -ExecutionPolicy Bypass -File "%BASE%ps1����\dnf_monitor.ps1"

echo ========================================
echo   DNF ������Ϸ - һ�������ű�
echo ========================================
echo.

echo [����] start-server.bat...
set "SRV_BAT="
for /f "delims=" %%i in ('dir /s /b "%BASE%ServerS4A12-AUM\start-server.bat" 2^>nul') do set "SRV_BAT=%%i"
if not defined SRV_BAT (
    echo [����] δ�ҵ� start-server.bat
    echo   ��ȷ�� ServerS4A12-AUM Ŀ¼�´��� start-server.bat
    echo   ��������ڣ�����?ServerUI ��ִ��һ�θ��¡�
    pause
    exit /b
)
echo   OK: !SRV_BAT!

echo.
echo [����] DNF.exe...
set "CLI_EXE="
for /f "delims=" %%i in ('dir /s /b "%BASE%DNF.exe" 2^>nul') do set "CLI_EXE=%%i"
if defined CLI_EXE goto found_cli
for /f "delims=" %%i in ('dir /s /b "%BASE%..\DNF.exe" 2^>nul') do set "CLI_EXE=%%i"
if defined CLI_EXE goto found_cli
for /f "delims=" %%i in ('dir /s /b "%BASE%..\..\DNF.exe" 2^>nul') do set "CLI_EXE=%%i"
if defined CLI_EXE goto found_cli
echo [����] δ�ҵ� DNF.exe
echo   ���ڽű�����Ŀ¼���ϼ�Ŀ¼���� DNF.exe ��Ϸ�ͻ��ˡ�
pause
exit /b

:found_cli
echo   OK: !CLI_EXE!
for %%i in ("!CLI_EXE!") do set "CLI_DIR=%%~dpi"

set "CLI_BAT="
if exist "!CLI_DIR!������ϷS4.bat" (
    set "CLI_BAT=!CLI_DIR!������ϷS4.bat"
) else if exist "!CLI_DIR!������Ϸ����.bat" (
    set "CLI_BAT=!CLI_DIR!������Ϸ����.bat"
)
if defined CLI_BAT (
    echo   �ͻ��˽ű�: !CLI_BAT!
)

echo.
echo [1/2] ���������?..
> "%temp%\dnf_srv.vbs" echo Set ws=CreateObject("WScript.Shell"^):ws.Run """!SRV_BAT!""",0,False
cscript //nologo "%temp%\dnf_srv.vbs"
del "%temp%\dnf_srv.vbs"

echo   �ȴ�����˾���?(�˿� 7001)...
set /a COUNT=0
:wait_srv
timeout /t 2 /nobreak >nul
netstat -an | findstr ":7001 .*LISTENING" >nul 2>&1
if not errorlevel 1 goto srv_ready
set /a COUNT+=2
if !COUNT! lss 30 goto wait_srv
echo [����] �ȴ���ʱ (30��)���˿� 7001 δ����
:srv_ready
echo   ������Ѿ���?(�ȴ� !COUNT! ��)
timeout /t 3 /nobreak >nul

echo.
echo [2/2] ������Ϸ�ͻ���...
if defined CLI_BAT (
    > "%temp%\dnf_cli.vbs" echo Set ws=CreateObject("WScript.Shell"^):ws.Run "cmd.exe /c cd /d ""!CLI_DIR!"" && ""!CLI_BAT!""",0,False
    cscript //nologo "%temp%\dnf_cli.vbs"
    del "%temp%\dnf_cli.vbs"
) else (
    > "%temp%\dnf_cli.vbs" echo Set ws=CreateObject("WScript.Shell"^):ws.Run """!CLI_EXE!"" 99?127.0.0.1?7001?10038?de509f65e9ccaae621cb7278fc2b8e6c?01?1?0?0?0?0?1?9n2b1c8r3w7y?0?0?19847",0,False
    cscript //nologo "%temp%\dnf_cli.vbs"
    del "%temp%\dnf_cli.vbs"
)

echo.
echo ========================================
echo   ��Ϸ������
echo ========================================
echo.

echo   dnf.exe��Ϸ�˳��󣬱����ڽ����Զ��رշ���ˣ����ǻ����ӳ٣��뾲��batָ�Ӧ
:wait_exit
timeout /t 3 /nobreak >nul
tasklist /fi "imagename eq DNF.exe" 2>nul | findstr /i "DNF.exe" >nul 2>&1
if not errorlevel 1 goto wait_exit

echo.
echo ��Ϸ���˳����رշ����?..
for /f "tokens=2 delims== " %%a in ('wmic process where "name='cmd.exe' and commandline like '%%start-server.bat%%'" get processid /value 2^>nul') do (
    taskkill /F /T /PID %%a >nul 2>&1
)
taskkill /f /im DfoServer.exe >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":7001 " 2^>nul') do (
    >nul 2>&1 taskkill /f /pid %%a
)
echo ������ѹر�?
timeout /t 2 /nobreak >nul
exit
