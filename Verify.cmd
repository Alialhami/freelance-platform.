@echo off
cd /d "%~dp0"
if not exist "App\FreelancePlatform.exe" (
  echo Run Build.cmd first.
  pause
  exit /b 1
)
start /wait "" "App\FreelancePlatform.exe" --self-test "verification-results.txt"
type "verification-results.txt"
pause
