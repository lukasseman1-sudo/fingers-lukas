@echo off
setlocal
set "MANIFEST=%~dp0FingersOpenXRLayer_implicit.json"
reg delete "HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit" /v "%MANIFEST%" /f
echo Fingers OpenXR layer registry value removal requested.
echo Close all VR applications and restart MSFS normally.
pause
