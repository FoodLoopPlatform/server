# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first (for better layer caching)
COPY src/FoodLoop.API/FoodLoop.API.csproj src/FoodLoop.API/
COPY src/FoodLoop.Application/FoodLoop.Application.csproj src/FoodLoop.Application/
COPY src/FoodLoop.Domain/FoodLoop.Domain.csproj src/FoodLoop.Domain/
COPY src/FoodLoop.Infrastructure/FoodLoop.Infrastructure.csproj src/FoodLoop.Infrastructure/

# Restore only the API project
RUN dotnet restore src/FoodLoop.API/FoodLoop.API.csproj

# Copy the remaining source code
COPY . .

# Publish the API
RUN dotnet publish src/FoodLoop.API/FoodLoop.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "FoodLoop.API.dll"]