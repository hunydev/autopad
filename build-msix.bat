@echo off
setlocal

echo ========================================
echo  AutoPad - MSIX Package Build (x64 + arm64)
echo ========================================
echo.

set SDK_BIN=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64
set PROJECT_DIR=%~dp0AutoPad
set BIN_DIR=%PROJECT_DIR%\bin
set PFX_PATH=%PROJECT_DIR%\autopad-dev.pfx
set PFX_PASSWORD=autopad
set BUNDLE_OUTPUT=%BIN_DIR%\AutoPad.msixbundle

cd /d "%PROJECT_DIR%"

echo [1/6] Cleaning previous build...
dotnet clean -c Release -v q 2>nul
if exist "%BIN_DIR%\bundle_staging" rmdir /s /q "%BIN_DIR%\bundle_staging"
mkdir "%BIN_DIR%\bundle_staging"

echo.
echo [2/6] Publishing x64 (self-contained)...
dotnet publish -c Release -r win-x64
if %ERRORLEVEL% NEQ 0 goto :error

set X64_PUBLISH=%BIN_DIR%\Release\net8.0-windows10.0.19041.0\win-x64\publish
set X64_MSIX=%BIN_DIR%\bundle_staging\AutoPad_x64.msix

:: Copy manifest (x64) and assets - manifest already has x64
copy /Y "%PROJECT_DIR%\Package.appxmanifest" "%X64_PUBLISH%\AppxManifest.xml" >nul
xcopy /Y /I "%PROJECT_DIR%\Assets" "%X64_PUBLISH%\Assets" >nul

echo.
echo [3/6] Creating x64 MSIX...
"%SDK_BIN%\makeappx.exe" pack /d "%X64_PUBLISH%" /p "%X64_MSIX%" /o
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo [4/6] Publishing arm64 (self-contained)...
dotnet publish -c Release -r win-arm64
if %ERRORLEVEL% NEQ 0 goto :error

set ARM64_PUBLISH=%BIN_DIR%\Release\net8.0-windows10.0.19041.0\win-arm64\publish
set ARM64_MSIX=%BIN_DIR%\bundle_staging\AutoPad_arm64.msix

:: Copy manifest (arm64) and assets - replace x64 with arm64
copy /Y "%PROJECT_DIR%\Package.appxmanifest" "%ARM64_PUBLISH%\AppxManifest.xml" >nul
powershell -NoProfile -Command "(Get-Content '%ARM64_PUBLISH%\AppxManifest.xml') -replace 'x64','arm64' | Set-Content '%ARM64_PUBLISH%\AppxManifest.xml'"
xcopy /Y /I "%PROJECT_DIR%\Assets" "%ARM64_PUBLISH%\Assets" >nul

echo.
echo [5/6] Creating arm64 MSIX...
"%SDK_BIN%\makeappx.exe" pack /d "%ARM64_PUBLISH%" /p "%ARM64_MSIX%" /o
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo [6/6] Creating MSIX Bundle and signing...
"%SDK_BIN%\makeappx.exe" bundle /d "%BIN_DIR%\bundle_staging" /p "%BUNDLE_OUTPUT%" /o
if %ERRORLEVEL% NEQ 0 goto :error

"%SDK_BIN%\signtool.exe" sign /fd SHA256 /a /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%BUNDLE_OUTPUT%"
if %ERRORLEVEL% NEQ 0 goto :error

:: Also keep individual signed MSIXes
"%SDK_BIN%\signtool.exe" sign /fd SHA256 /a /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%X64_MSIX%"
"%SDK_BIN%\signtool.exe" sign /fd SHA256 /a /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%ARM64_MSIX%"

:: Copy individual msix to bin root for convenience
copy /Y "%X64_MSIX%" "%BIN_DIR%\AutoPad_x64.msix" >nul
copy /Y "%ARM64_MSIX%" "%BIN_DIR%\AutoPad_arm64.msix" >nul

echo.
echo ========================================
echo  MSIX Bundle Build SUCCESS!
echo.
echo  Bundle:  %BUNDLE_OUTPUT%
echo  x64:     %BIN_DIR%\AutoPad_x64.msix
echo  arm64:   %BIN_DIR%\AutoPad_arm64.msix
echo ========================================
goto :end

:error
echo.
echo ========================================
echo  MSIX Build FAILED!
echo ========================================

:end
endlocal
pause
