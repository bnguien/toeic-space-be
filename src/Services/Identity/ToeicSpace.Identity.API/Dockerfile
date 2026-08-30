FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/Identity/ToeicSpace.Identity.API/ToeicSpace.Identity.API.csproj", "src/Services/Identity/ToeicSpace.Identity.API/"]
COPY ["src/Services/Identity/ToeicSpace.Identity.Application/ToeicSpace.Identity.Application.csproj", "src/Services/Identity/ToeicSpace.Identity.Application/"]
COPY ["src/Services/Identity/ToeicSpace.Identity.Domain/ToeicSpace.Identity.Domain.csproj", "src/Services/Identity/ToeicSpace.Identity.Domain/"]
COPY ["src/Services/Identity/ToeicSpace.Identity.Infrastructure/ToeicSpace.Identity.Infrastructure.csproj", "src/Services/Identity/ToeicSpace.Identity.Infrastructure/"]
COPY ["src/BuildingBlocks/ToeicSpace.BuildingBlocks/ToeicSpace.BuildingBlocks.csproj", "src/BuildingBlocks/ToeicSpace.BuildingBlocks/"]
COPY ["src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/ToeicSpace.BuildingBlocks.Messaging.csproj", "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Messaging/"]

RUN dotnet restore "src/Services/Identity/ToeicSpace.Identity.API/ToeicSpace.Identity.API.csproj"

COPY . .

WORKDIR "/src/src/Services/Identity/ToeicSpace.Identity.API"

RUN dotnet build \
    "ToeicSpace.Identity.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish \
    "ToeicSpace.Identity.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ToeicSpace.Identity.API.dll"]
