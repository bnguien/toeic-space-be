FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/Assessment/ToeicSpace.Assessment.API/ToeicSpace.Assessment.API.csproj", "src/Services/Assessment/ToeicSpace.Assessment.API/"]
COPY ["src/Services/Assessment/ToeicSpace.Assessment.Application/ToeicSpace.Assessment.Application.csproj", "src/Services/Assessment/ToeicSpace.Assessment.Application/"]
COPY ["src/Services/Assessment/ToeicSpace.Assessment.Domain/ToeicSpace.Assessment.Domain.csproj", "src/Services/Assessment/ToeicSpace.Assessment.Domain/"]
COPY ["src/Services/Assessment/ToeicSpace.Assessment.Infrastructure/ToeicSpace.Assessment.Infrastructure.csproj", "src/Services/Assessment/ToeicSpace.Assessment.Infrastructure/"]
COPY ["src/BuildingBlocks/ToeicSpace.BuildingBlocks/ToeicSpace.BuildingBlocks.csproj", "src/BuildingBlocks/ToeicSpace.BuildingBlocks/"]
COPY ["src/BuildingBlocks/ToeicSpace.BuildingBlocks.Security/ToeicSpace.BuildingBlocks.Security.csproj", "src/BuildingBlocks/ToeicSpace.BuildingBlocks.Security/"]

RUN dotnet restore "src/Services/Assessment/ToeicSpace.Assessment.API/ToeicSpace.Assessment.API.csproj"

COPY . .

WORKDIR "/src/src/Services/Assessment/ToeicSpace.Assessment.API"

RUN dotnet build \
    "ToeicSpace.Assessment.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish \
    "ToeicSpace.Assessment.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ToeicSpace.Assessment.API.dll"]
