@echo off
pushd "%~dp0"
call build || goto :end
if not exist dist mkdir dist || goto :end
for %%p in (*.nuspec) do nuget pack -OutputDirectory "dist" %%p %* || goto :end
:end
popd
