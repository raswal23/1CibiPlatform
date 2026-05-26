#PRODUCTION
# Stage 1: Build Blazor WASM
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG ENVIRONMENT=Production

WORKDIR /src

# Install Python required by the WebAssembly toolchain
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && ln -sf /usr/bin/python3 /usr/bin/python \
    && rm -rf /var/lib/apt/lists/*

# Install wasm-tools workload required for Blazor WebAssembly on .NET 10
RUN dotnet workload install wasm-tools --skip-manifest-update

# Copy project file and restore dependencies
COPY FrontendWebassembly.csproj ./
RUN dotnet restore "./FrontendWebassembly.csproj"
RUN dotnet workload restore "./FrontendWebassembly.csproj"

# Copy all source files
COPY . ./

# Remove host build outputs that can break Linux restore/publish inside container
RUN find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

# Restore again after source copy/cleanup to regenerate Linux assets
RUN dotnet restore "./FrontendWebassembly.csproj"
RUN dotnet workload restore "./FrontendWebassembly.csproj"

# Publish Blazor WASM (outputs to wwwroot inside publish folder)
RUN dotnet publish "./FrontendWebassembly.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Swap the environment-specific appsettings file
RUN echo "Configuring appsettings for ENVIRONMENT = $ENVIRONMENT" && \
    if [ "$ENVIRONMENT" = "UAT" ]; then \
      echo "Using UAT configuration..." && \
      rm -f /app/publish/wwwroot/appsettings.Production.json && \
      mv /app/publish/wwwroot/appsettings.UAT.json /app/publish/wwwroot/appsettings.Production.json && \
      rm -f /app/publish/wwwroot/appsettings.Development.json; \
    elif [ "$ENVIRONMENT" = "Development" ]; then \
      echo "Using Development configuration..." && \
      rm -f /app/publish/wwwroot/appsettings.Production.json && \
      mv /app/publish/wwwroot/appsettings.Development.json /app/publish/wwwroot/appsettings.Production.json && \
      rm -f /app/publish/wwwroot/appsettings.UAT.json; \
    else \
      echo "Using Production configuration..." && \
      rm -f /app/publish/wwwroot/appsettings.Development.json && \
      rm -f /app/publish/wwwroot/appsettings.UAT.json; \
    fi && \
    echo "Final appsettings files:" && \
    ls -la /app/publish/wwwroot/appsettings* && \
    echo "Content of appsettings.Production.json:" && \
    cat /app/publish/wwwroot/appsettings.Production.json && \
    echo "=========================================="

# Stage 2: Serve with Nginx
FROM nginx:alpine AS final

# Remove default Nginx content
RUN rm -rf /usr/share/nginx/html/*

# Copy Blazor published wwwroot files
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Copy custom Nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 8080
EXPOSE 8080

# Start Nginx in foreground
CMD ["nginx", "-g", "daemon off;"]
