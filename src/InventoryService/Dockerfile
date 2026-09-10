FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/InventoryService/InventoryService.csproj", "src/InventoryService/"]
RUN dotnet restore "src/InventoryService/InventoryService.csproj"

COPY . .
WORKDIR /src/src/InventoryService
RUN dotnet publish "InventoryService.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "InventoryService.dll"]
