# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . ./
WORKDIR /app/AlexaGPT

RUN dotnet publish -c Release -o out

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/AlexaGPT/out ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "AlexaGPT.dll"]