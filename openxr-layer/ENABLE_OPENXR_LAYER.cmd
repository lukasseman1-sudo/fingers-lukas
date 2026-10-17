@echo off
setlocal
set "MANIFEST=%~dp0FingersOpenXRLayer_implicit.json"
if not exist "%~dp0FingersOpenXRLayer.dll" (
 echo ERROR: Missing FingersOpenXRLayer.dll
 pause
 exit /b 2
)
if not exist "%MANIFEST%" (
 echo ERROR: Missing implicit layer manifest
 pause
 exit /b 2
)
reg add "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%MANIFEST%" /t REG_DWORD /d 0 /f
if errorlevel 1 (echo Registration failed & pause & exit /b 1)
echo Experimental Fingers OpenXR layer registered for this Windows user.
echo IMPORTANT: Stop MSFS before enabling; start it normally in Steam.
echo To disable, run DISABLE_OPENXR_LAYER.cmd
pause
