@echo off
setlocal
pushd "%~dp0"
call build
set EXIT_CODE=%ERRORLEVEL%
if not %EXIT_CODE%==0 goto :end
for %%f in (3.0 2.2 2.1) do (
    dotnet publish --no-restore --no-build -c Release -f netcoreapp%%f -o dist\bin\%%f src || goto :break
)
:break
set EXIT_CODE=%ERRORLEVEL%
:end
popd
exit /b %EXIT_CODE%
