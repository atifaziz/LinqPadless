@echo off
setlocal
set PORT=%~1
set SERVE_ARGS=
set DOCKER_PORT_ARG=
if defined PORT (
    set DOCKER_PORT_ARG=-p %PORT%:%PORT%
    set SERVE_ARGS=-a 0.0.0.0:%PORT%
)
docker run --rm -it -v "%cd%:/docs" %DOCKER_PORT_ARG% ^
           squidfunk/mkdocs-material serve %SERVE_ARGS%
