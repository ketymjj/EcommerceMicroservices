# =============== STOCK SERVICE ===============
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS stockservice-build
WORKDIR /src

# Copia projetos mantendo estrutura
COPY Shared/ ./Shared/
COPY StockService/ ./StockService/

# Restaura dependências
WORKDIR /src/StockService
RUN dotnet restore StockService.csproj

# Build
RUN dotnet build StockService.csproj -c Release -o /app/build

# Publicação
RUN dotnet publish StockService.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=stockservice-build /app/publish .
ENTRYPOINT ["dotnet", "StockService.dll"]




