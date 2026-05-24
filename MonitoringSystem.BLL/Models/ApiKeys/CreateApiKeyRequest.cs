namespace MonitoringSystem.BLL.Models.ApiKeys;

public class CreateApiKeyRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
}