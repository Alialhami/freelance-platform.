@echo off
cd /d "%~dp0"
if not exist "App\FreelancePlatform.exe" (
  echo The compiled app is missing. Run Build.cmd first.
  pause
  exit /b 1
)
start "" "App\FreelancePlatform.exe"
