FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/ApiGateways/ToeicSpace.ApiGateway/ToeicSpace.ApiGateway.csproj", "src/ApiGateways/ToeicSpace.ApiGateway/"]

RUN dotnet restore "src/ApiGateways/ToeicSpace.ApiGateway/ToeicSpace.ApiGateway.csproj"

COPY . .

WORKDIR "/src/src/ApiGateways/ToeicSpace.ApiGateway"

RUN dotnet publish \
    "ToeicSpace.ApiGateway.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ToeicSpace.ApiGateway.dll"]
