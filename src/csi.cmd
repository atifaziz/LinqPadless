@echo off
setlocal
set LINQPADLESS=__LINQPADLESS__
for %%i in (csi.exe) do set CSIPATH=%%~$PATH:i
if defined CSIPATH goto :boot
if defined ProgramFiles(x86) set CSIPATH=%ProgramFiles(x86)%\MSBuild\14.0\Bin\csi.exe && goto :boot
set CSIPATH=%ProgramFiles%\MSBuild\14.0\Bin\csi.exe
:boot
if not exist "%CSIPATH%" goto :nocsi
pushd "%~dp0"
:: __PACKAGES__
:run
popd
if %errorlevel%==0 "%CSIPATH%" "%~dpn0.csx" -- %*
goto :EOF

:pkgerr
popd
exit /b %errorlevel%

:nocsi
echo Microsoft (R) Visual C# Interactive Compiler does not appear to be
echo installed. You can download it as part of Microsoft Build Tools 2015
echo using the URL below, install and try again:
echo https://www.microsoft.com/en-us/download/details.aspx?id=48159
exit /b 1
