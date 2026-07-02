chcp 65001
@echo off
cd /d "%~dp0"
if exist "ServerUI-无依赖版.exe" (start "" "ServerUI-无依赖版.exe") else if exist "ServerUI-依赖版.exe" (start "" "ServerUI-依赖版.exe") else (echo ServerUI not found.&pause)
