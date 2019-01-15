@echo off
pushd "%~dp0"
call build && dotnet publish --no-restore --no-build -c Release -o ..\dist\bin src
popd
