@echo off
setlocal
set LINQPADLESS=__LINQPADLESS__
set CSIPATH=%~dp0packages\Microsoft.Net.Compilers.2.0.1\tools\csi.exe
pushd "%~dp0"
if not exist "%CSIPATH%" nuget install Microsoft.Net.Compilers -Version 2.0.1 -OutputDirectory packages >&2 || goto :pkgerr
:: __PACKAGES__
:run
popd
if %errorlevel%==0 "%CSIPATH%" "%~dpn0.csx" -- %*
goto :EOF

:nocsi
>&2 echo Microsoft (R) Visual C# Interactive Compiler does not appear to be
>&2 echo installed. You can download it as part of Microsoft Build Tools 2015
>&2 echo using the URL below, install and try again:
>&2 echo https://www.microsoft.com/en-us/download/details.aspx?id=48159
exit /b 1

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
