# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy everything
COPY ["Autoscaler.Api/", "Autoscaler.Api/"]
COPY ["Autoscaler.Persistence/", "Autoscaler.Persistence/"]
COPY ["Autoscaler.Runner/", "Autoscaler.Runner/"]
# Restore as distinct layers

WORKDIR /App/Autoscaler.Persistence
RUN dotnet restore
RUN dotnet build

WORKDIR /App/Autoscaler.Runner
RUN dotnet restore
RUN dotnet build

# Install Node.js
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/* 

WORKDIR /App/Autoscaler.Api
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App

# Install Node.js
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/* 

# Copy application files
COPY --from=build-env /out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Autoscaler.Api.dll"]
