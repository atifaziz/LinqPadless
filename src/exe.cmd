@echo off
setlocal
set LINQPADLESS=__LINQPADLESS__
pushd "%~dp0"
:: __PACKAGES__
popd
if %errorlevel%==0 "%~dpn0.exe" %*
goto :EOF

:pkgerr
set err=%errorlevel%
set NUGETPATH=
for %%i in (nuget.exe) do set NUGETPATH=%%~$PATH:i
if not defined NUGETPATH call :nonuget
popd
exit /b %err%

:nonuget
>&2 echo NuGet does not appear to be installed, which is needed to restore
>&2 echo missing dependencies. Visit the following URLs for instructions on how
>&2 echo to obtain the command-line version of NuGet:
>&2 echo - https://docs.nuget.org/consume/installing-nuget#command-line-utility
>&2 echo - https://dist.nuget.org/index.html
exit /b 1
