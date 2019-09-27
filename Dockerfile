ARG DOTNET_SDK_VERSION=2.2.402
ARG PLATFORM=alpine3.9

FROM mcr.microsoft.com/dotnet/core/sdk:3.0.100-${PLATFORM} AS build

ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

COPY . /project

WORKDIR /project

RUN dotnet pack -c Release \
 && mkdir download \
 && wget -O - https://github.com/atifaziz/LinqPadlessProgramTemplate/tarball/master | tar -xz -C download \
 && mv download/* tmpl

FROM mcr.microsoft.com/dotnet/core/sdk:${DOTNET_SDK_VERSION}-${PLATFORM} AS tool

COPY --from=build /project/dist/*.nupkg /dist/
COPY --from=build /project/etc/sourceless-nuget.config nuget.config

RUN dotnet tool install --global \
        --configfile nuget.config \
        --add-source dist \
        LinqPadless --version 2.0.0

FROM mcr.microsoft.com/dotnet/core/sdk:${DOTNET_SDK_VERSION}-${PLATFORM}

COPY --from=tool /root/.dotnet/tools /root/.dotnet/tools

ENV PATH=$PATH:/root/.dotnet/tools

COPY --from=build /project/tmpl/.lpless /scripts/.lpless
COPY --from=build /project/tmpl/.lplessroot /scripts

RUN mkdir /scripts/linq

WORKDIR /scripts/linq

ENTRYPOINT [ "lpless" ]
