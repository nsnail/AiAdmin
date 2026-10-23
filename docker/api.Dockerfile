FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . .

# 构建上下文不含 .git，MinVer 无法计算版本，由工作流传入 tag 名（可带 v 前缀）
ARG APP_VERSION=0.0.0-container

RUN dotnet publish src/AiAdmin.Api/AiAdmin.Api.csproj \
    --configuration Release \
    --output /app/publish \
    -p:MinVerSkip=true \
    -p:Version=${APP_VERSION#v} \
    -p:RunAnalyzers=false \
    -p:EnforceCodeStyleInBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AiAdmin.Api.dll", "--enabled-hosted-services"]