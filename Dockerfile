# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy everything
COPY ./Autoscaler .
# Restore as distinct layers
RUN dotnet restore

# Install Node.js
RUN apt-get update && apt-get install -y python3 curl && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Build and publish a release
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App

# Install Node.js and Python
RUN apt-get update && apt-get install -y python3 python3-pip python3-venv curl && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/* 

# Create and activate a Python virtual environment
RUN python3 -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy application files
COPY --from=build-env /App/out .
COPY ./Autoscaler/model ./model
COPY ./Autoscaler/predict.py .
COPY ./Autoscaler/train.py .
COPY ./Autoscaler/utils.py .
COPY ./requirements.txt .

# Install Python dependencies into the virtual environment
RUN pip install --no-cache-dir -r requirements.txt

ENTRYPOINT ["dotnet", "Autoscaler.dll", "--scaler", "./predict.py", "--re-trainer", "./train.py"]
