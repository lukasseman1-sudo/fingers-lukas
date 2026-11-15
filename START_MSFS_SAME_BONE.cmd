@echo off
setlocal
cd /d "%~dp0"
set "FINGERS_MSFS=1"
set "FINGERS_MOUSE_ONLY=0"
set "FINGERS_MSFS_SAME_BONE_ABSOLUTE=1"
if not exist "FingersApp.exe" (
  echo ERROR: FingersApp.exe is missing next to this launcher.
  pause
  exit /b 1
)
start "" "%~dp0FingersApp.exe"
