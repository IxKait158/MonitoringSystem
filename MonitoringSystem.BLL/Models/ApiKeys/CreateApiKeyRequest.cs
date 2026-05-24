namespace MonitoringSystem.BLL.Models.ApiKeys;

public class CreateApiKeyRequest
{
    public string ServiceName { get; set; }
    public string Owner { get; set; }
}