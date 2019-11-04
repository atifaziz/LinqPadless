@echo off
pushd "%~dp0"
call :main %*
popd
goto :EOF

:main
setlocal
set TEST=dotnet test --no-build -c
call build && %TEST% Debug && %TEST% Release
goto :EOF
