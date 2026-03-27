# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BackendAPI/API/APIs/APIs.csproj", "BackendAPI/API/APIs/"]
COPY ["BackendAPI/Modules/Auth/Auth.csproj", "BackendAPI/Modules/Auth/"]
COPY ["BackendAPI/BuildingBlocks/BuildingBlocks/BuildingBlocks.csproj", "BackendAPI/BuildingBlocks/BuildingBlocks/"]
COPY ["BackendAPI/Modules/CNX/CNX.csproj", "BackendAPI/Modules/CNX/"]
COPY ["BackendAPI/Modules/PhilSys/PhilSys.csproj", "BackendAPI/Modules/PhilSys/"]
COPY ["BackendAPI/Modules/SSO/SSO.csproj", "BackendAPI/Modules/SSO/"]
COPY ["BackendAPI/Modules/AIAgent/AIAgent.csproj", "BackendAPI/Modules/AIAgent/"]
RUN dotnet restore "./BackendAPI/API/APIs/APIs.csproj"
COPY . .
WORKDIR "/src/BackendAPI/API/APIs"
RUN dotnet build "./APIs.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./APIs.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# ⭐ IMPORTANT: Copy Skills directory with .skill.yaml files
# dotnet publish doesn't include non-compiled files like .yaml by default
COPY --from=build /src/BackendAPI/Modules/AIAgent/Skills ./Modules/AIAgent/Skills

# --- FIX: Ensure wwwroot and runtime folders are writable by container user ---
# Temporarily switch to root to fix ownership and permissions
USER root
RUN mkdir -p /app/wwwroot/files \
    && chown -R $APP_UID:$APP_UID /app/wwwroot \
    && find /app/wwwroot -type d -exec chmod 755 {} + \
    && find /app/wwwroot -type f -exec chmod 644 {} +

# Switch back to the non-root container user
USER $APP_UID

ENTRYPOINT ["dotnet", "APIs.dll"]