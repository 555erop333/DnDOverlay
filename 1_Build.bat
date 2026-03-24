@echo off
setlocal
cd /d "%~dp0"

set "PUBLISH_DIR=publish\Release\win-x64"
set "PROJECT=DnDOverlay.App\DnDOverlay.App.csproj"

echo Stopping running application...
taskkill /F /IM DnDOverlay.App.exe 2>nul
timeout /t 1 /nobreak >nul

echo Cleaning previous build...
if exist "%PUBLISH_DIR%" (
    rd /s /q "%PUBLISH_DIR%"
)

echo Publishing application (self-contained single file)...
dotnet publish "%PROJECT%" --configuration Release --runtime win-x64 --self-contained true --output "%PUBLISH_DIR%"

if not %ERRORLEVEL%==0 goto :publish_failed

echo.
echo Copying media folders (Audio, Music, Background)...
for %%D in (Audio Music Background) do (
    if exist "%%D" (
        echo   %%D -> %PUBLISH_DIR%\%%D
        xcopy "%%D" "%PUBLISH_DIR%\%%D\" /E /I /Y >nul
    ) else (
        echo   Skipping %%D (not found)
    )
)

echo Ensuring Data folder exists in release...
if exist "Data" (
    xcopy "Data" "%PUBLISH_DIR%\Data\" /E /I /Y >nul
) else (
    if not exist "%PUBLISH_DIR%\Data" mkdir "%PUBLISH_DIR%\Data"
)

echo.
echo Build completed successfully!
echo Location: %PUBLISH_DIR%\DnDOverlay.App.exe
goto :eof

:publish_failed
echo.
echo Build failed!

:eof
echo.
pause
endlocal
