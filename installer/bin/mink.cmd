@echo off
setlocal
set "MINK_EXE=%~dp0..\minK.exe"
for %%A in (%*) do (
    if /I "%%~A"=="-v" goto :sync
    if /I "%%~A"=="--version" goto :sync
    if /I "%%~A"=="-h" goto :sync
    if /I "%%~A"=="--help" goto :sync
    if /I "%%~A"=="-w" goto :sync
    if /I "%%~A"=="--wait" goto :sync
)
start "" /B "%MINK_EXE%" %*
exit /b 0

:sync
"%MINK_EXE%" %*
exit /b %ERRORLEVEL%