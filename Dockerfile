FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ConnectaOficios.Domain/ConnectaOficios.Domain.csproj ConnectaOficios.Domain/
COPY ConnectaOficios.Api/ConnectaOficios.Api.csproj ConnectaOficios.Api/
COPY docker/DatabaseInit/DatabaseInit.csproj docker/DatabaseInit/
RUN dotnet restore ConnectaOficios.Api/ConnectaOficios.Api.csproj \
    && dotnet restore docker/DatabaseInit/DatabaseInit.csproj
COPY ConnectaOficios.Domain/ ConnectaOficios.Domain/
COPY ConnectaOficios.Api/ ConnectaOficios.Api/
COPY docker/DatabaseInit/ docker/DatabaseInit/
RUN dotnet publish ConnectaOficios.Api/ConnectaOficios.Api.csproj -c Release --no-restore -o /out/api /p:UseAppHost=false \
    && dotnet publish docker/DatabaseInit/DatabaseInit.csproj -c Release --no-restore -o /out/init /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /out/api/ ./
COPY --from=build /out/init/ ./init/
COPY docker/appsettings.json ./appsettings.json
COPY docker/entrypoint.sh ./entrypoint.sh
USER app
ENTRYPOINT ["/bin/sh", "/app/entrypoint.sh"]
