# Multi-stage build: SPA (node) -> server (dotnet) -> runtime.
# Build context is the repo root: docker build -f docker/Dockerfile .

FROM node:22-alpine AS web
WORKDIR /web
COPY CSharp/web/package*.json ./
RUN npm ci
COPY CSharp/web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY CSharp/src/SlotDemo.Server/ SlotDemo.Server/
RUN dotnet publish SlotDemo.Server -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=server /app ./
COPY --from=web /web/dist ./wwwroot/
ENV ASPNETCORE_URLS=http://0.0.0.0:5090
EXPOSE 5090
ENTRYPOINT ["dotnet", "SlotDemo.Server.dll"]
