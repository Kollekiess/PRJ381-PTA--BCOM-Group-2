@echo off
setlocal
set "PROJECT_ROOT=%~dp0.."
set "APP_BINARY=%PROJECT_ROOT%\unity\Builds\Windows\BCSignVR Standalone.exe"

if not exist "%APP_BINARY%" (
  echo The Windows classroom build was not found.
  echo Open the Unity project and choose BCSignVR ^> Build Windows 11 Demo first.
  exit /b 1
)

where py >nul 2>&1
if errorlevel 1 (
  set "PYTHON=python"
) else (
  set "PYTHON=py -3"
)

start "BCSignVR Backend" /min cmd /c "cd /d ""%PROJECT_ROOT%\backend"" ^&^& %PYTHON% -m bcsignvr.server"
start "BCSignVR Webcam" /min cmd /c "cd /d ""%PROJECT_ROOT%"" ^&^& %PYTHON% webcam\hand_bridge.py --camera auto --no-preview"
start /wait "" "%APP_BINARY%"

taskkill /FI "WINDOWTITLE eq BCSignVR Backend" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq BCSignVR Webcam" /T /F >nul 2>&1
