FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
COPY Examples /app
WORKDIR /app/OnPremise/Mixed/GettingStarted-API
ARG CONFIG=Release
RUN dotnet publish -c "$CONFIG" 

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble
RUN apt-get update && apt-get install -y --no-install-recommends libatomic1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY ip-intelligence-data/51Degrees-EnterpriseIpiV41.ipi .
COPY --from=build /app/OnPremise/Mixed/GettingStarted-API/bin/Release/net8.0/publish .
CMD ["dotnet", "GettingStarted-API.dll"]
EXPOSE 5225
