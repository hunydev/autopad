@echo off
setlocal

echo ========================================
echo  AutoPad - Release Build ^& Publish
echo ========================================
echo.

cd /d "%~dp0AutoPad"

echo [1/3] Cleaning previous build...
dotnet clean -c Release -v q

echo.
echo [2/3] Publishing (self-contained, single file)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o "%~dp0publish\self-contained"

echo.
echo [3/3] Publishing (framework-dependent, single file)...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%~dp0publish\single"

echo.
if %ERRORLEVEL% EQU 0 (
    echo ========================================
    echo  Build SUCCESS!
    echo.
    echo  [self-contained] .NET 런타임 포함, 단독 실행
    echo    %~dp0publish\self-contained\AutoPad.exe
    echo.
    echo  [single] .NET 8 런타임 필요, 경량 배포
    echo    %~dp0publish\single\AutoPad.exe
    echo ========================================
) else (
    echo ========================================
    echo  Build FAILED!
    echo ========================================
)

endlocal
pause
