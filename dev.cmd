@echo off
rem Dev loop: double-click this instead of build-release.cmd while coding.
rem - Web UI (UI\web\src): served by vite with HMR, edits appear in the running app, no rebuild.
rem - C# / XAML: dotnet watch hot-reloads in place where the runtime allows, else restarts the app.
rem Uses a dev database (%APPDATA%\Clip\clip.dev.db) so real history is untouched.
rem Release Clip must exit first (single-instance lock); this script stops it and offers it back at the end.
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

if not exist "UI\web\node_modules" (
    echo Installing web dependencies - once only...
    pushd "%~dp0UI\web"
    call npm.cmd install
    if errorlevel 1 (
        popd
        echo npm install failed. Fix the error above, then run this again.
        pause
        exit /b 1
    )
    popd
)

echo Stopping release Clip so the dev copy owns the single-instance lock...
taskkill /IM Clip.exe /F >nul 2>&1
set tries=0
:devwait
tasklist /FI "IMAGENAME eq Clip.exe" 2>nul | find /i "Clip.exe" >nul
if errorlevel 1 goto devstarted
set /a tries+=1
if %tries% GEQ 10 goto devstarted
ping -n 2 127.0.0.1 >nul
goto devwait
:devstarted

rem ---- vite dev server: reuse one already listening, else start one (minimized window) ----
call :probe5173
if not errorlevel 1 goto viteup

start "Clip web (vite dev - close me when done)" /min /D "%~dp0UI\web" npm.cmd run dev -- --port 5173 --strictPort

echo Waiting for the web server on http://localhost:5173/ ...
powershell -NoProfile -Command "$d=[DateTime]::UtcNow; while(([DateTime]::UtcNow-$d).TotalSeconds -lt 90){ try { $r=[System.Net.WebRequest]::Create('http://localhost:5173/'); $r.Timeout=1500; $r.GetResponse().Close(); exit 0 } catch { if ($_.Exception.Response) { exit 0 } }; Start-Sleep -Milliseconds 500 }; exit 1"
if errorlevel 1 (
    echo WARNING: no web server on port 5173 after 90s. The app will open blank;
    echo fix the vite window error, then press Ctrl+R in the Clip window to reload.
) else (
    echo Web server is up.
)
goto webdone

:viteup
echo Using the dev server already running on http://localhost:5173/ - not starting another.
:webdone

rem ---- C# with hot reload; rude edits restart automatically, no prompt ----
set CLIP_WEB_URL=http://localhost:5173/
set DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
dotnet watch run --project Clip.csproj -- --db "%APPDATA%\Clip\clip.dev.db"

echo.
echo Dev session ended. The vite window is still open - close it when done coding.
if exist "%~dp0dist\Clip.exe" (
    echo Starting release Clip again...
    start "" "%~dp0dist\Clip.exe"
)
pause
goto :eof

:probe5173
rem True (errorlevel 0) when something answers HTTP on 5173; "localhost" covers IPv4 and IPv6
rem (a TcpClient probe misses a vite server bound only to ::1).
powershell -NoProfile -Command "try { $r=[System.Net.WebRequest]::Create('http://localhost:5173/'); $r.Timeout=1500; $r.GetResponse().Close(); exit 0 } catch { if ($_.Exception.Response) { exit 0 } }; exit 1"
exit /b %errorlevel%
