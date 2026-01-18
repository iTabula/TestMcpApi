namespace KamMobile.Models;

public class VapiConfiguration
{
    public string PrivateApiKey { get; set; } = string.Empty;
    public string AssistantId { get; set; } = string.Empty;
}

public class McpConfiguration
{
    public string SseEndpoint { get; set; } = string.Empty;
}

public class QuestionRequest
{
    public string Question { get; set; } = string.Empty;
}