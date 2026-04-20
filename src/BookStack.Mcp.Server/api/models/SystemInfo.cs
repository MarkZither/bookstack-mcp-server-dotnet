namespace BookStack.Mcp.Server.Api.Models;

public sealed class SystemInfo
{
    public string Version { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string PhpVersion { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string AppUrl { get; set; } = string.Empty;
    public bool DrawingEnabled { get; set; }
    public bool RegistrationsEnabled { get; set; }
    public int UploadLimit { get; set; }
}
