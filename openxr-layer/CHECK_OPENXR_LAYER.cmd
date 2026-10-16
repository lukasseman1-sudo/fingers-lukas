@echo off
setlocal
set "MANIFEST=%~dp0FingersOpenXRLayer_implicit.json"
echo === Fingers OpenXR diagnostics ===
echo Manifest expected: "%MANIFEST%"
if exist "%MANIFEST%" (echo Manifest: PRESENT) else (echo Manifest: MISSING)
if exist "%~dp0FingersOpenXRLayer.dll" (echo DLL: PRESENT) else (echo DLL: MISSING)
echo.
echo === Registered implicit layers ===
reg query "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" 2>nul
echo.
echo === Checking Fingers registration ===
reg query "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%MANIFEST%" 2>nul
if errorlevel 1 echo ERROR: Current folder manifest is NOT registered.
echo.
echo If the registry shows a different Fingers directory, run the
echo corresponding DISABLE_OPENXR_LAYER.cmd from the old directory first.
echo The current DLL may also be rejected by the OpenXR loader.
pause
