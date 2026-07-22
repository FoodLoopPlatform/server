# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

COPY FoodLoop.sln ./

COPY src/FoodLoop.API/FoodLoop.API.csproj src/FoodLoop.API/
COPY src/FoodLoop.Application/FoodLoop.Application.csproj src/FoodLoop.Application/
COPY src/FoodLoop.Domain/FoodLoop.Domain.csproj src/FoodLoop.Domain/
COPY src/FoodLoop.Infrastructure/FoodLoop.Infrastructure.csproj src/FoodLoop.Infrastructure/

RUN dotnet restore FoodLoop.sln

COPY . .

RUN dotnet publish src/FoodLoop.API/FoodLoop.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=builder /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "FoodLoop.API.dll"]