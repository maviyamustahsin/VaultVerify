# Use the official .NET 8 SDK as a build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the project file and restore dependencies
COPY ResetApp/BasicResetApp.csproj ./ResetApp/
RUN dotnet restore ResetApp/BasicResetApp.csproj

# Copy the remaining files and build the app
COPY . .
RUN dotnet publish ResetApp/BasicResetApp.csproj -c Release -o out

# Use the official .NET runtime as a runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
RUN chown -R 1000:1000 /app
USER 1000

# Use a persistent SQLite database path or define it in appsettings.json
# For simplicity, we'll use the embedded app.db
ENV ASPNETCORE_URLS=http://+:7860

EXPOSE 7860

ENTRYPOINT ["dotnet", "BasicResetApp.dll"]
