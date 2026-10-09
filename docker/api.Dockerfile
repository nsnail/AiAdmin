FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . .

RUN dotnet publish src/AiAdmin.Api/AiAdmin.Api.csproj \
    --configuration Release \
    --output /app/publish \
    -p:MinVerSkip=true \
    -p:Version=0.0.0-container \
    -p:RunAnalyzers=false \
    -p:EnforceCodeStyleInBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AiAdmin.Api.dll", "--enabled-hosted-services"]