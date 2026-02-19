FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DmsSearch.Api/DmsSearch.Api.csproj", "DmsSearch.Api/"]
COPY ["DmsSearch.Application/DmsSearch.Application.csproj", "DmsSearch.Application/"]
COPY ["DmsSearch.Domain/DmsSearch.Domain.csproj", "DmsSearch.Domain/"]
COPY ["DmsSearch.Infrastructure/DmsSearch.Infrastructure.csproj", "DmsSearch.Infrastructure/"]
RUN dotnet restore "DmsSearch.Api/DmsSearch.Api.csproj"
COPY . .
WORKDIR "/src/DmsSearch.Api"
RUN dotnet build "./DmsSearch.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DmsSearch.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# uploads klasörünü APP_UID kullanıcısına ait olarak oluştur
USER root
RUN mkdir -p /app/uploads && chown -R $APP_UID /app/uploads
USER $APP_UID

ENTRYPOINT ["dotnet", "DmsSearch.Api.dll"]
