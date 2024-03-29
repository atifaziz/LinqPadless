ARG DOTNET_SDK_VERSION=8.0.100
ARG PLATFORM=alpine3.18

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-1-${PLATFORM} AS build

ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

COPY . /project

WORKDIR /project

RUN dotnet pack -c Release src

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-1-${PLATFORM} AS tool

COPY --from=build /project/dist/*.nupkg /dist/
COPY --from=build /project/etc/sourceless-nuget.config nuget.config

RUN dotnet tool install --global \
        --configfile nuget.config \
        --add-source dist \
        LinqPadless --version 2.0.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-1-${PLATFORM}

COPY --from=tool /root/.dotnet/tools /root/.dotnet/tools

ENV PATH=$PATH:/root/.dotnet/tools

COPY --from=build /project/.lpless /scripts/.lpless
COPY --from=build /project/.lplessroot /scripts

RUN mkdir /scripts/linq

WORKDIR /scripts/linq

ENTRYPOINT [ "lpless" ]
