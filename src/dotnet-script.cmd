@echo off
setlocal
set DOTNET_SCRIPT_PATH=
for %%x in (com exe bat cmd) do if exist "%~dp0dotnet-script.%%x" set DOTNET_SCRIPT_PATH=%~dp0dotnet-script.%%x
if not defined DOTNET_SCRIPT_PATH for %%i in (dotnet-script.exe) do set DOTNET_SCRIPT_PATH=%%~$PATH:i
if not defined DOTNET_SCRIPT_PATH call :no-dotnet-script
set LINQPADLESS=__LINQPADLESS__
call "%DOTNET_SCRIPT_PATH%" "%~dpn0.csx" -- %*
goto :EOF

:no-dotnet-script
>&2 echo dotnet-script does not appear to be installed, which is needed to
>&2 echo run this script. For instructions on how to download and install,
>&2 echo visit: https://github.com/filipw/dotnet-script
exit /b 1
