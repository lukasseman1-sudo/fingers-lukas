@echo off
setlocal
cd /d "%~dp0"
set "FINGERS_MSFS=1"
set "FINGERS_MOUSE_ONLY=0"
set "FINGERS_MSFS_SAME_BONE_ABSOLUTE=1"

if not exist "%~dp0FingersApp.exe" (
  echo ERROR: FingersApp.exe is missing next to this launcher.
  pause
  exit /b 1
)

rem The GitHub build runner does not have the user's Ultraleap SDK.
rem Use LeapC.dll from the locally installed Gemini SDK instead.
if not exist "%~dp0LeapC.dll" (
  if exist "%ProgramFiles%\Ultraleap\LeapSDK\lib\x64\LeapC.dll" (
    copy /Y "%ProgramFiles%\Ultraleap\LeapSDK\lib\x64\LeapC.dll" "%~dp0LeapC.dll" >nul
    if errorlevel 1 (
      echo ERROR: Could not copy LeapC.dll from Ultraleap SDK.
      echo Try running from a writable folder, e.g. Desktop.
      pause
      exit /b 1
    )
  ) else (
    echo ERROR: LeapC.dll was not found.
    echo Expected: "%ProgramFiles%\Ultraleap\LeapSDK\lib\x64\LeapC.dll"
    echo Install the Ultraleap Hand Tracking Software SDK, or copy
    echo LeapC.dll from your working Fingers installation into this folder.
    echo No LeapC.dll means Fingers cannot connect to the Ultraleap camera.
    pause
    exit /b 1
  )
)

start "" "%~dp0FingersApp.exe"
