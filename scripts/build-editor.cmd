@echo off
rem Rebuilds Assets\editor\editor.bundle.js. Never required for the C# build.
cd /d "%~dp0editor"
call npm.cmd install
call npm.cmd run build
pause
