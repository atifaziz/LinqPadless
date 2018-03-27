@echo off
pushd "%~dp0"
call :main %*
popd
goto :EOF

:main
setlocal
set VERSION_SUFFIX=
if not "%~1"=="" set VERSION_SUFFIX=--version-suffix %~1
call build                                               ^
 && dotnet pack -c Release %VERSION_SUFFIX%
goto :EOF
