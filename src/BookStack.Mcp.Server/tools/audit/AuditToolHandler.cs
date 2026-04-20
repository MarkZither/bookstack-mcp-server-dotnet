using System.ComponentModel;
using BookStack.Mcp.Server.Api;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace BookStack.Mcp.Server.Tools.Audit;

[McpServerToolType]
internal sealed class AuditToolHandler(IBookStackApiClient client, ILogger<AuditToolHandler> logger)
{
    private readonly IBookStackApiClient _client = client;
    private readonly ILogger<AuditToolHandler> _logger = logger;

    [McpServerTool(Name = "bookstack_audit_log_list"), Description("List audit log entries from BookStack")]
    public Task<string> ListAuditLogAsync(CancellationToken ct)
        => throw new NotImplementedException("Implemented in Issue #10");
}
