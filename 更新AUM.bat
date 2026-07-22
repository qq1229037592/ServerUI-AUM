@echo off
title AUM-Manager-SelfUpdate
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0更新AUM.ps1"
pause
