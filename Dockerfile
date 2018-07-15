FROM microsoft/dotnet:2.0-sdk AS build
ENV NET_CORE_VERSION="netcoreapp2.0"
WORKDIR /build
COPY . ./
RUN dotnet --info && \
    dotnet publish BakaChan/BakaChan.csproj -c Release -f "$NET_CORE_VERSION" -o out /nologo && \
    cp "BakaChan/scripts/BakaChan.sh" "BakaChan/out/BakaChan.sh"

FROM microsoft/dotnet:2.0-runtime
LABEL maintainer="Sophie Tauchert <sophie@999eagle.moe>"
WORKDIR /app
COPY --from=build /build/BakaChan/out ./
RUN echo "deb http://deb.debian.org/debian buster main" >> /etc/apt/sources.list && \
    apt-get update && \
	apt-get -y install libopus0/stable libc6/testing
ENTRYPOINT ["./BakaChan.sh"]
