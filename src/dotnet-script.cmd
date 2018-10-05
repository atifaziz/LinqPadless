@echo off
setlocal
set DOTNET_SCRIPT_PATH=
for %%i in (dotnet-script.exe) do set DOTNET_SCRIPT_PATH=%%~$PATH:i
if not defined DOTNET_SCRIPT_PATH call :no-dotnet-script
set LINQPADLESS=__LINQPADLESS__
"%DOTNET_SCRIPT_PATH%" "%~dpn0.csx" -- %*
goto :EOF

:no-dotnet-script
>&2 echo dotnet does not appear to be installed, which is needed to run this
>&2 echo script. For instructions on how to download and install, visit:
>&2 echo https://github.com/filipw/dotnet-script
exit /b 1
