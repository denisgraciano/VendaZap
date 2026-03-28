FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/VendaZap.API/VendaZap.API.csproj", "src/VendaZap.API/"]
COPY ["src/VendaZap.Infrastructure/VendaZap.Infrastructure.csproj", "src/VendaZap.Infrastructure/"]
COPY ["src/VendaZap.Application/VendaZap.Application.csproj", "src/VendaZap.Application/"]
COPY ["src/VendaZap.Domain/VendaZap.Domain.csproj", "src/VendaZap.Domain/"]
RUN dotnet restore "src/VendaZap.API/VendaZap.API.csproj"

COPY . .
WORKDIR "/src/src/VendaZap.API"
RUN dotnet build "VendaZap.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "VendaZap.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "VendaZap.API.dll"]
