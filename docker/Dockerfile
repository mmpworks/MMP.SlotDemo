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

# The server does not stand alone. It has a project reference to MMP.SlotGame.Core,
# and its csproj pulls two sets of files in as content: the shipped games, which the
# labs and the PAR sheet load, and the article markdown that the TEACH ME section
# serves. Copying only the server directory produced a publish that failed on every
# Core type, so the layout below mirrors the repo closely enough for those relative
# paths to resolve.
COPY CSharp/Directory.Build.props CSharp/
COPY CSharp/src/ CSharp/src/
COPY CSharp/games/ CSharp/games/
COPY docs/articles/ docs/articles/

RUN dotnet publish CSharp/src/SlotDemo.Server -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=server /app ./
# Program.cs looks for the built SPA at ../../web/dist first, for a dev checkout, then
# falls back to wwwroot, which is what this is.
COPY --from=web /web/dist ./wwwroot/
ENV ASPNETCORE_URLS=http://0.0.0.0:5090
EXPOSE 5090
ENTRYPOINT ["dotnet", "SlotDemo.Server.dll"]
