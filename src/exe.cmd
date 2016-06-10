@echo off
setlocal
set LINQPADLESS=__LINQPADLESS__
pushd "%~dp0"
:: __PACKAGES__
popd
if %errorlevel%==0 "%~dpn0.exe" %*
goto :EOF

:pkgerr
popd
exit /b %errorlevel%
