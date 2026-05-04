# The dotnet publish is run on the CI runner (not inside Docker) to avoid OOM.
# CI places the publish output in docker-publish/ before calling docker build.
# For local builds, run first:
#   dotnet publish src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj \
#     -c Release -o docker-publish --no-self-contained
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY docker-publish/ .

ENV BOOKSTACK_MCP_TRANSPORT=http
ENV BOOKSTACK_MCP_HTTP_PORT=3000

EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:3000/health || exit 1

ENTRYPOINT ["dotnet", "BookStack.Mcp.Server.dll"]
