@echo off
setlocal EnableExtensions
set "PROJECT_ROOT=%~dp0.."
set "APP_BINARY=%PROJECT_ROOT%\unity\Builds\Windows\BCSignVR Standalone.exe"

if not exist "%APP_BINARY%" (
  echo The Windows classroom build was not found.
  echo Open the Unity project and choose BCSignVR ^> Build Windows 11 Demo first.
  exit /b 1
)

set "PYTHON_EXE="
set "PYTHON_ARGS="
if exist "%PROJECT_ROOT%\.venv\Scripts\python.exe" set "PYTHON_EXE=%PROJECT_ROOT%\.venv\Scripts\python.exe"
if not defined PYTHON_EXE (
  where py >nul 2>&1
  if not errorlevel 1 (
    set "PYTHON_EXE=py"
    set "PYTHON_ARGS=-3"
  )
)
if not defined PYTHON_EXE (
  where python >nul 2>&1
  if not errorlevel 1 set "PYTHON_EXE=python"
)
if not defined PYTHON_EXE (
  echo Python 3.11 or newer is required for the backend and webcam bridge.
  exit /b 1
)

"%PYTHON_EXE%" %PYTHON_ARGS% -c "import cv2, mediapipe" >nul 2>&1
if errorlevel 1 (
  echo Webcam dependencies are missing. Run: "%PYTHON_EXE%" %PYTHON_ARGS% -m pip install -r webcam\requirements.txt
  exit /b 1
)

start "signzzZ Backend" /min cmd /c "cd /d ""%PROJECT_ROOT%\backend"" ^&^& ""%PYTHON_EXE%"" %PYTHON_ARGS% -m bcsignvr.server"
start "signzzZ Webcam" /min cmd /c "cd /d ""%PROJECT_ROOT%"" ^&^& ""%PYTHON_EXE%"" %PYTHON_ARGS% webcam\hand_bridge.py --camera auto --no-preview"
start /wait "" "%APP_BINARY%"

taskkill /FI "WINDOWTITLE eq signzzZ Backend" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq signzzZ Webcam" /T /F >nul 2>&1
