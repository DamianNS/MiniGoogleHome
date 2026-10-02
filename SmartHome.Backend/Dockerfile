FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SmartHome.Backend/SmartHome.Backend.csproj", "SmartHome.Backend/"]
COPY ["SmartHome.Shared/SmartHome.Shared.csproj", "SmartHome.Shared/"]
RUN dotnet restore "SmartHome.Backend/SmartHome.Backend.csproj"

COPY . /tmp/src
RUN rsync -av --dry-run /tmp/src/ /src/
#COPY . .
WORKDIR "/src/SmartHome.Backend"
RUN dotnet publish "SmartHome.Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# LAS DEPENDENCIAS DEBEN INSTALARSE EN LA IMAGEN FINAL (RUNTIME)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        mpv \
        python3 \
        alsa-utils \
        curl \
        ca-certificates && \
    curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp && \
    chmod a+rx /usr/local/bin/yt-dlp && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENV DISPLAY=:0
EXPOSE 5010
ENTRYPOINT ["dotnet", "SmartHome.Backend.dll"]
