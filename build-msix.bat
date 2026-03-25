@echo off
setlocal

echo ========================================
echo  AutoPad - MSIX Package Build
echo ========================================
echo.

set SDK_BIN=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64
set PROJECT_DIR=%~dp0AutoPad
set PUBLISH_DIR=%PROJECT_DIR%\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish
set MSIX_OUTPUT=%PROJECT_DIR%\bin\AutoPad.msix
set PFX_PATH=%PROJECT_DIR%\autopad-dev.pfx
set PFX_PASSWORD=autopad

cd /d "%PROJECT_DIR%"

echo [1/4] Cleaning previous build...
dotnet clean -c Release -v q

echo.
echo [2/4] Publishing (self-contained)...
dotnet publish -c Release
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo [3/4] Preparing MSIX package...
:: Copy manifest and assets to publish directory
copy /Y "%PROJECT_DIR%\Package.appxmanifest" "%PUBLISH_DIR%\AppxManifest.xml" >nul
xcopy /Y /I "%PROJECT_DIR%\Assets" "%PUBLISH_DIR%\Assets" >nul

:: Create MSIX package
"%SDK_BIN%\makeappx.exe" pack /d "%PUBLISH_DIR%" /p "%MSIX_OUTPUT%" /o
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo [4/4] Signing MSIX package...
"%SDK_BIN%\signtool.exe" sign /fd SHA256 /a /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%MSIX_OUTPUT%"
if %ERRORLEVEL% NEQ 0 goto :error

echo.
echo ========================================
echo  MSIX Build SUCCESS!
echo.
echo  Output: %MSIX_OUTPUT%
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
