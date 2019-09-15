ARG DOTNET_SDK_VERSION=2.2.402
ARG PLATFORM=-alpine3.9

FROM mcr.microsoft.com/dotnet/core/sdk:${DOTNET_SDK_VERSION}${PLATFORM} AS build

ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

COPY . /project

# workaround for error NETSDK1053 ("Pack as tool does not support self contained")

RUN mv project/src/LinqPadless.csproj tmp \
 && cat tmp/LinqPadless.csproj | grep -v PackAsTool > project/src/LinqPadless.csproj \
 && dotnet publish -c Release --self-contained -r linux-musl-x64 -o /app project/src \
 && mkdir download \
 && wget -O - https://github.com/atifaziz/LinqPadlessProgramTemplate/tarball/master | tar -xz -C download \
 && mv download/* tmpl

FROM mcr.microsoft.com/dotnet/core/sdk:2.2.402-alpine3.9

COPY --from=build /app /app
COPY --from=build /tmpl/.lpless /scripts/.lpless
COPY --from=build /tmpl/.lplessroot /scripts

RUN mkdir /scripts/linq

WORKDIR /scripts/linq

ENTRYPOINT [ "dotnet", "/app/lpless.dll" ]
