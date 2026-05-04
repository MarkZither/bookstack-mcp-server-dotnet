FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BookStack.Mcp.Server.sln global.json ./
COPY src/ src/

RUN dotnet publish src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV BOOKSTACK_MCP_TRANSPORT=http
ENV BOOKSTACK_MCP_HTTP_PORT=3000

EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:3000/health || exit 1

ENTRYPOINT ["dotnet", "BookStack.Mcp.Server.dll"]
