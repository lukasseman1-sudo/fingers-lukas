@echo off
rem Experimental Steam launch wrapper. Does not write registry/system environment.
rem Steam Launch Options (quote full path):
rem "C:\path\to\Fingers\OPENXR_MSFS_STEAM.cmd" %command%
if "%~1"=="" (
  echo No Steam game command provided.
  echo Set Steam Launch Options to: "%~f0" %%command%%
  pause
  exit /b 2
)
set "XR_API_LAYER_PATH=%~dp0"
set "XR_ENABLE_API_LAYERS=XR_APILAYER_FINGERS_headpose"
echo Starting MSFS with Fingers OpenXR pose layer enabled for this process only.
%*
exit /b %ERRORLEVEL%
