@echo off
rem Double-click this to build the latest Clip.exe into dist\ and dist-fd\ and start it.
rem Flow: build the web bundle if stale -> stop every running Clip -> publish one self-contained exe (dist) + one framework-dependent folder (dist-fd) -> start it.
rem If the product is renamed (AssemblyName in Clip.csproj), update the Clip.exe names below.
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET 10 SDK not found. Install it from https://dotnet.microsoft.com/download then run this again.
    pause
    exit /b 1
)
where npm.cmd >nul 2>&1
if errorlevel 1 (
    echo npm was not found. Install Node.js, then run "npm install" in UI\web and try again.
    pause
    exit /b 1
)

rem ---- 1. the web UI: rebuild only when missing or older than its sources -------------
set webbuild=0
powershell -NoProfile -ExecutionPolicy Bypass -Command "$d=Get-Item 'UI\web\dist\index.html' -ErrorAction SilentlyContinue; if(-not $d){exit 1}; $in=@(Get-ChildItem 'UI\web\src' -Recurse -File)+@(Get-Item 'UI\web\index.html','UI\web\package.json','UI\web\package-lock.json','UI\web\vite.config.ts','UI\web\tsconfig.json' -ErrorAction SilentlyContinue); if(@($in|Where-Object{$_.LastWriteTimeUtc -gt $d.LastWriteTimeUtc}).Count -gt 0){exit 1}; exit 0"
if errorlevel 1 set webbuild=1

if "%webbuild%"=="1" (
    echo Building the web UI - this takes a minute...
    pushd "%~dp0UI\web"
    call npm.cmd run build
    if errorlevel 1 (
        popd
        goto webfailed
    )
    popd
)
if not exist "%~dp0UI\web\dist\index.html" goto webfailed

rem ---- 2. stop every running copy ----------------------------------------------------
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
rem ---- 3. clear last build outputs so stale files never linger ---------------------------
rem Runs after the stop above: deleting dist while Clip is still running would fail on a locked exe.
if exist "%~dp0dist" rmdir /s /q "%~dp0dist"
if exist "%~dp0dist-fd" rmdir /s /q "%~dp0dist-fd"
rem ---- 4. publish the self-contained exe ---------------------------------------------
echo Building Clip - this takes a minute...
echo.
dotnet publish Clip.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none ^
    -o dist
if errorlevel 1 goto failed

for %%F in ("%~dp0dist\Clip.exe") do set exesize=%%~zF
findstr /M /C:"clip.assets.web/index.html" "%~dp0dist\Clip.exe" >nul
if errorlevel 1 (
    echo ERROR: the published exe does not contain the web page. Nothing was started.
    echo Fix the embed in Clip.csproj, then run this script again.
    pause
    exit /b 1
)
echo.
echo Embedded page: clip.assets.web/index.html
echo Portable exe: dist\Clip.exe ^(%exesize% bytes, runs with no .NET installed^)

rem ---- 5. framework-dependent folder (needs the .NET 10 desktop runtime on the target machine) --
echo Building the framework-dependent folder - this takes a minute...
dotnet publish Clip.csproj -c Release --self-contained false ^
    -p:DebugType=none ^
    -o dist-fd
if errorlevel 1 goto failed

for /f %%S in ('powershell -NoProfile -Command "(Get-ChildItem dist-fd -Recurse -File | Measure-Object Length -Sum).Sum"') do set fdsize=%%S
echo Runtime folder: dist-fd\Clip.exe + files beside it ^(%fdsize% bytes total^)

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

:webfailed
echo.
echo BUILD FAILED: the web UI could not be built, so there is no page to embed.
echo Fix the error above (try "npm install" in UI\web), then run this script again.
pause
exit /b 1

:failed
echo.
echo BUILD FAILED. Outputs were cleared before the build, so restart Clip from your
echo Start-menu shortcut if you need it running while you fix the error above.
if exist "%~dp0dist\Clip.exe" start "" "%~dp0dist\Clip.exe"
echo Fix the error above, then run this script again.
pause
exit /b 1
