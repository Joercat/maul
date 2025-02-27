# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Create new web project
RUN dotnet new web --name MaulingSimulator
WORKDIR /src/MaulingSimulator

# Copy your source files
COPY Program.cs .
COPY MaulingSimulator.csproj .
COPY wwwroot/ ./wwwroot/

# Build the project
RUN dotnet build -c Release
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

# Copy built app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV PORT=5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Create non-root user
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "MaulingSimulator.dll"]
