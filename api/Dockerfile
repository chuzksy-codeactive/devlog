# Build context: api/ (docker build --file api/Dockerfile api).
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY DevLog.Domain/DevLog.Domain.csproj DevLog.Domain/
COPY DevLog.Infrastructure/DevLog.Infrastructure.csproj DevLog.Infrastructure/
COPY DevLog.Api/DevLog.Api.csproj DevLog.Api/
RUN dotnet restore DevLog.Api/DevLog.Api.csproj

COPY DevLog.Domain/ DevLog.Domain/
COPY DevLog.Infrastructure/ DevLog.Infrastructure/
COPY DevLog.Api/ DevLog.Api/
RUN dotnet publish DevLog.Api/DevLog.Api.csproj \
    --configuration Release \
    --no-restore \
    --no-self-contained \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "DevLog.Api.dll"]
