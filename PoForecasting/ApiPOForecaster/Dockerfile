# escape=`

# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)

# Base (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-windowsservercore-ltsc2022 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install VC++ runtime (needed by native ML.NET deps)
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]
RUN New-Item -ItemType Directory -Force -Path C:\temp | Out-Null; Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile 'C:\temp\vc_redist.x64.exe'; Start-Process -FilePath 'C:\temp\vc_redist.x64.exe' -ArgumentList '/install','/quiet','/norestart' -Wait; Remove-Item 'C:\temp\vc_redist.x64.exe' -Force


# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0-windowsservercore-ltsc2022 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["ApiPOForecaster/ApiPOForecaster.csproj", "ApiPOForecaster/"]
COPY ["Core/Core.csproj", "Core/"]
RUN dotnet restore "ApiPOForecaster/ApiPOForecaster.csproj"

COPY . .
WORKDIR /src/ApiPOForecaster
RUN dotnet build "ApiPOForecaster.csproj" -c %BUILD_CONFIGURATION% -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ApiPOForecaster.csproj" -c %BUILD_CONFIGURATION% -o /app/publish /p:UseAppHost=false

# Final (production) - inherits base, so VC++ is already installed
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .


ENTRYPOINT ["dotnet", "ApiPOForecaster.dll"]