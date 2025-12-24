# ===== build stage =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY WebAPIChatAI.csproj .
RUN dotnet restore WebAPIChatAI.csproj

COPY . .
RUN dotnet publish WebAPIChatAI.csproj -c Release -o /app/publish

# ===== runtime stage =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WebAPIChatAI.dll"]
