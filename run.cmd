@echo off
pushd "%~dp0"
echo>&2 WARNING! Working directory is: %~dp0
dotnet run --no-launch-profile -f net8.0 --project src -- %*
popd
