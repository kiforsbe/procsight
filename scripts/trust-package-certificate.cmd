@echo off
setlocal DisableDelayedExpansion
if "%~1"=="" goto usage
if not "%~2"=="" goto usage
where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo PowerShell 7.4 or later is required. Install it, then drop the certificate again.
  pause
  exit /b 1
)
pwsh.exe -NoLogo -NoProfile -File "%~dp0trust-package-certificate.ps1" -CertificatePath "%~1"
set "result=%errorlevel%"
echo.
pause
exit /b %result%
:usage
echo Drop one public .cer file onto this script to trust its signing certificate.
echo This adds machine-wide Trusted People trust after administrator approval.
pause
exit /b 1
