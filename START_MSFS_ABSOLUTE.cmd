@echo off
setlocal
cd /d "%~dp0"
set FINGERS_MSFS=1
set FINGERS_MSFS_ABSOLUTE=1
set FINGERS_MOUSE_ONLY=0
if exist "FingersApp.exe" (
 start "" "FingersApp.exe"
) else if exist "bin\Release\net48\FingersApp.exe" (
 start "" "bin\Release\net48\FingersApp.exe"
) else (
 echo FingersApp.exe not found. Build the Windows project first.
 pause
)
