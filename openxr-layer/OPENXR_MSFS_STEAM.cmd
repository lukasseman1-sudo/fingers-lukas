@echo off
setlocal
rem Steam Launch Options example:
rem cmd /c ""C:\full\path\OPENXR_MSFS_STEAM.cmd" %command%"
set "LOG=%~dp0steam-launch.log"
>>"%LOG%" echo ==== Steam wrapper started %date% %time% ====
if "%~1"=="" (
  >>"%LOG%" echo ERROR: Steam game command not supplied.
  echo ERROR: No Steam game command supplied. See "%LOG%"
  exit /b 2
)
if not exist "%~dp0FingersApp.exe" (
  >>"%LOG%" echo ERROR: FingersApp.exe missing.
  exit /b 3
)
set "XR_API_LAYER_PATH=%~dp0"
set "XR_ENABLE_API_LAYERS=XR_APILAYER_FINGERS_headpose"
rem Fingers.exe has a single-instance guard; start only if absent.
tasklist /FI "IMAGENAME eq FingersApp.exe" /NH | find /I "FingersApp.exe" >nul
if errorlevel 1 (
  >>"%LOG%" echo Starting Fingers in MSFS mode.
  set "FINGERS_MSFS=1"
  start "" "%~dp0FingersApp.exe"
)
>>"%LOG%" echo Launching Steam supplied game command.
%*
set "CODE=%ERRORLEVEL%"
>>"%LOG%" echo Game command returned %CODE%.
exit /b %CODE%
