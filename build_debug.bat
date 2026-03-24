@echo off
setlocal enabledelayedexpansion
pushd "%~dp0"

echo Cleaning Debug build...
taskkill /IM DnDOverlay.App.exe /F >nul 2>&1
dotnet clean -c Debug || goto :error

echo Building Debug configuration...
dotnet build -c Debug || goto :error

echo.
echo Done. Launching Debug executable...
set APP_PATH=%~dp0DnDOverlay.App\bin\Debug\net5.0-windows\DnDOverlay.App.exe
if exist "%APP_PATH%" (
    start "DnDOverlay Debug" "%APP_PATH%"
) else (
    echo Debug executable not found at:
    echo    %APP_PATH%
)

popd
echo.
pause
exit /b 0

:error
set CODE=%ERRORLEVEL%
echo.
echo Build script failed with exit code %CODE%.
popd
echo.
pause
exit /b %CODE%
