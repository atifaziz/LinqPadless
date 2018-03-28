@echo off
setlocal
set DOTNETPATH=
for %%i in (dotnet.exe) do set DOTNETPATH=%%~$PATH:i
if not defined DOTNETPATH call :no-dotnet
set LINQPADLESS=__LINQPADLESS__
pushd "%~dp0"
dotnet run -p "~dpn0"-- %*
popd
goto :EOF

:no-dotnet
>&2 echo dotnet CLI does not appear to be installed, which is needed to build
>&2 echo and run the script. Visit https://dot.net/ for instruction on how to
>&2 echo download and install.
exit /b 1
