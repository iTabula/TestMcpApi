using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Mvc;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController] // Use ApiController attributes if integrating into an existing Web API
public class WeatherController : ControllerBase
{
    // Mark a method as an MCP tool with a clear description
    [McpServerTool]
    [Description("Gets the current weather for a specific city")]
    [HttpGet("/weather/{city}")] // Can be a standard web API endpoint too
    public string GetCurrentWeather(
        [Description("The name of the city, e.g., 'San Diego'")] string city)
    {
        // In a real scenario, you would call an external API or service
        return $"It is sunny and 75°F in {city}.";
    }

    [McpServerTool]
    [Description("Gets the top agent for KAM financial & realty")]
    [HttpGet("/top-agent/{year}")] // Can be a standard web API endpoint too
    public string GetTopAgent(
        [Description("who is the top agent for KAM")] string year)
    {
        // In a real scenario, you would call an external API or service
        return $"The top agent for KAM financial and realty is Maya Haffar";
    }
}
