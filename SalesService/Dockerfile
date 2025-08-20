# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia os csproj primeiro para restaurar dependências
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["SalesService/SalesService.csproj", "SalesService/"]

RUN dotnet restore "SalesService/SalesService.csproj"

# Copia todo o resto do código
COPY . .

WORKDIR /src/SalesService
RUN dotnet build "SalesService.csproj" -c Release -o /app/build

# Stage 2: publish
FROM build AS publish
RUN dotnet publish "SalesService.csproj" -c Release -o /app/publish

# Stage 3: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SalesService.dll"]
