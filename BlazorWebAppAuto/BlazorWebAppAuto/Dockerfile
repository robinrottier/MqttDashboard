# Build stage — needs full SDK to compile all projects
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Python (required by the wasm-tools workload for AOT compilation)
RUN apt-get update && apt-get install -y python3 && ln -sf /usr/bin/python3 /usr/bin/python && rm -rf /var/lib/apt/lists/*

# Install the WASM workload required for Blazor WebAssembly builds
RUN dotnet workload install wasm-tools

# Copy all project files (for layer caching on package restore)
COPY BlazorApp1/BlazorApp1.csproj                                                     BlazorApp1/
COPY BlazorApp1.Server/BlazorApp1.Server.csproj                                       BlazorApp1.Server/
COPY BlazorWebAppAuto/BlazorWebAppAuto.Client/BlazorWebAppAuto.Client.csproj          BlazorWebAppAuto/BlazorWebAppAuto.Client/
COPY BlazorWebAppAuto/BlazorWebAppAuto/BlazorWebAppAuto.csproj                         BlazorWebAppAuto/BlazorWebAppAuto/

# Restore dependencies (after workload install so WASM targets are available)
RUN dotnet restore BlazorWebAppAuto/BlazorWebAppAuto/BlazorWebAppAuto.csproj

# Copy the rest of the source
COPY BlazorApp1/            BlazorApp1/
COPY BlazorApp1.Server/     BlazorApp1.Server/
COPY BlazorWebAppAuto/      BlazorWebAppAuto/

# Publish the server project (this also builds & includes the WASM client)
RUN dotnet publish BlazorWebAppAuto/BlazorWebAppAuto/BlazorWebAppAuto.csproj \
    -c Release \
    -o /app/publish

# Runtime stage — lightweight ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create data directory for diagram persistence
RUN mkdir -p /app/data

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "BlazorWebAppAuto.dll"]
