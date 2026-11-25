FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StockFlow.Api/StockFlow.Api.csproj StockFlow.Api/
RUN dotnet restore StockFlow.Api/StockFlow.Api.csproj

COPY StockFlow.Api/ StockFlow.Api/

RUN dotnet publish StockFlow.Api/StockFlow.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /out/ ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "StockFlow.Api.dll"]
