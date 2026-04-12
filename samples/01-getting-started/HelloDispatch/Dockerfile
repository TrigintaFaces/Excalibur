# JIT Dockerfile — HelloDispatch Getting Started Sample
# See: docs-site/docs/advanced/container-deployment.md §2.1
#
# Build:  docker build -t hello-dispatch .
# Run:    docker run --rm hello-dispatch

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore (layer caching)
COPY ["HelloDispatch.csproj", "."]
RUN dotnet restore

# Copy source and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Run as non-root
USER app

# Health probe port (if using WebHost)
EXPOSE 8080

ENTRYPOINT ["dotnet", "HelloDispatch.dll"]
