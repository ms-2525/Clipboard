@echo off
rem Double-click this to build the latest Clip.exe into dist\ and start it.
rem Flow: stop every running Clip -> wait until none are left -> build -> stop any stray copy -> start exactly one.
rem If the product is renamed (AssemblyName in Clip.csproj), update the Clip.exe names below.
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET 10 SDK not found. Install it from https://dotnet.microsoft.com/download then run this again.
    pause
    exit /b 1
)

echo Stopping every running copy of Clip...
taskkill /IM Clip.exe /F >nul 2>&1
set tries=0
:waitloop
tasklist /FI "IMAGENAME eq Clip.exe" 2>nul | find /i "Clip.exe" >nul
if errorlevel 1 goto stopped
set /a tries+=1
if %tries% GEQ 15 goto stillrunning
ping -n 2 127.0.0.1 >nul
goto waitloop

:stillrunning
echo Could not stop Clip. Right-click its icon in the notification area, choose Exit, then run this again.
pause
exit /b 1

:stopped
echo Building Clip - this takes a minute...
echo.
dotnet publish Clip.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none ^
    -o dist
if errorlevel 1 goto failed

rem Something may have started old copies during the build (Windows startup entry, a shortcut); they would win Clip's
rem single-instance lock and the new exe would exit. Clear them again right before starting.
taskkill /IM Clip.exe /F >nul 2>&1
ping -n 2 127.0.0.1 >nul

echo.
echo Starting %~dp0dist\Clip.exe
start "" "%~dp0dist\Clip.exe"
ping -n 4 127.0.0.1 >nul

rem Clip owns a single-instance lock, so this also reports exactly one copy is up (the path proves it is the fresh one).
for /f "delims=" %%p in ('powershell -NoProfile -Command "(Get-Process Clip -ErrorAction SilentlyContinue | Select-Object -First 1).Path"') do set running=%%p
if /i "%running%"=="%~dp0dist\Clip.exe" (
    echo Clip is running from dist. One instance only.
) else if not defined running (
    echo WARNING: Clip did not start. Details in %APPDATA%\Clip\crash.log
) else (
    echo WARNING: the copy running is "%running%" - not the one just built.
    echo Exit it from the notification area, then run this script again.
)

rem Keep "start with Windows" pointing at the exe this script builds, not the old bin folder.
reg query "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v Clip >nul 2>&1
if not errorlevel 1 reg add "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v Clip /t REG_SZ /d "%~dp0dist\Clip.exe" /f >nul
pause
exit /b 0

:failed
echo.
echo BUILD FAILED. Restarting the last good exe so you still have Clip.
if exist "%~dp0dist\Clip.exe" start "" "%~dp0dist\Clip.exe"
echo Fix the error above, then run this script again.
pause
exit /b 1
