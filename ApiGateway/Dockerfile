# =============== API GATEWAY ===============
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS apigateway-build
WORKDIR /src

# Copia apenas o csproj para cache do restore
COPY ["ApiGateway.csproj", "./"]

# Restaura dependências
RUN dotnet restore "ApiGateway.csproj"

# Copia todo o restante do código
COPY . .

# Build
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

# Publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=apigateway-build /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]


