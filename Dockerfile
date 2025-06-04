# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Install Node.js
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/* && \
    mkdir -p /App

WORKDIR /App/Autoscaler.Persistence
COPY Autoscaler.Persistence .
RUN dotnet restore
RUN dotnet build

WORKDIR /App/Autoscaler.Runner
COPY Autoscaler.Runner .
RUN dotnet restore
RUN dotnet build

WORKDIR /App/
COPY Autoscaler.Api .
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
