# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /source

# Copy csproj and restore dependencies
COPY ["MaulingSimulator.csproj", "./"]
RUN dotnet restore

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

# Copy built app
COPY --from=build /app ./

# Copy static files
COPY ["wwwroot", "./wwwroot"]

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV PORT=5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Create non-root user for security
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Expose port
EXPOSE 5000

# Set entry point
ENTRYPOINT ["dotnet", "MaulingSimulator.dll"]
