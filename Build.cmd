@echo off
cd /d "%~dp0"
dotnet publish "FreelancePlatform.csproj" -c Release --self-contained false -o App
if errorlevel 1 (
  echo Build failed. Check that the .NET 10 SDK is installed.
  pause
  exit /b 1
)
echo Build complete. Run Run.cmd.
pause
