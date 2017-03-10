@echo off
setlocal
if "%PROCESSOR_ARCHITECTURE%"=="x86" set MSBUILD=%ProgramFiles%
if defined ProgramFiles(x86) set MSBUILD=%ProgramFiles(x86)%
set MSBUILD=%MSBUILD%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe
if exist "%MSBUILD%" goto :restore
set MSBUILD=
for %%i in (MSBuild.exe) do set MSBUILD=%%~dpnx$PATH:i
if not defined MSBUILD goto :nomsbuild
set MSBUILD_VERSION_MAJOR=
set MSBUILD_VERSION_MINOR=
for /f "delims=. tokens=1,2,3,4" %%m in ('msbuild /version /nologo') do (
    set MSBUILD_VERSION_MAJOR=%%m
    set MSBUILD_VERSION_MINOR=%%n
)
if not defined MSBUILD_VERSION_MAJOR goto :nomsbuild
if not defined MSBUILD_VERSION_MINOR goto :nomsbuild
if %MSBUILD_VERSION_MAJOR% lss 15    goto :nomsbuild
if %MSBUILD_VERSION_MINOR% lss 1     goto :nomsbuild
:restore
for %%i in (NuGet.exe) do set nuget=%%~dpnx$PATH:i
if "%nuget%"=="" (
    echo WARNING! NuGet executable not found in PATH so build may fail!
    echo For more on NuGet, see https://github.com/nuget/home
)
pushd "%~dp0"
nuget restore ^
 && call :build Debug   %* ^
 && call :build Release %*
popd
goto :EOF

:build
setlocal
"%MSBUILD%" /p:Configuration=%1 /v:m %2 %3 %4 %5 %6 %7 %8 %9
goto :EOF

:nomsbuild
echo Microsoft Build version 15.1 (or later) does not appear to be
echo installed on this machine, which is required to build the solution.
exit /b 1
