# The API for the Demo environment on Railway (docs/deploy-railway.md). Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# The committed OpenAPI document is not regenerated here: that would start the app without a database.
RUN dotnet publish src/Pinta.Api/Pinta.Api.csproj -c Release -o /app \
    -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Kestrel listens on 8080 on every interface, IPv6 included (Railway's private network).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Pinta.Api.dll"]
