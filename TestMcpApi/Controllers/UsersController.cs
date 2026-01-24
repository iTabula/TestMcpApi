using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Models;
using TestMcpApi.Services;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UsersController(IUserService userService, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        svc = userService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        _httpContextAccessor = httpContextAccessor;
    }

    [McpServerTool]
    [Description("Get all users")]
    [HttpGet("/users/all")]
    public async Task<IActionResult> GetAllUsers(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = await svc.GetUsers();
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get user by ID")]
    [HttpGet("/users/by-id")]
    public async Task<IActionResult> GetUserById(
        [Description("The user ID to look up")] int userId,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var user = await svc.GetUserById(userId);
        if (user == null)
            return NotFound($"User with ID {userId} not found.");

        return Ok(user);
    }

    [McpServerTool]
    [Description("Search users by name (full name, first name, or last name)")]
    [HttpGet("/users/by-name")]
    public IActionResult GetUsersByName(
        [Description("The name to search for")] string name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetByName(name);
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get user by email address")]
    [HttpGet("/users/by-email")]
    public IActionResult GetUserByEmail(
        [Description("The email address to look up")] string email,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var user = svc.GetByEmail(email);
        if (user == null)
            return NotFound($"User with email {email} not found.");

        return Ok(user);
    }

    [McpServerTool]
    [Description("Get users by city")]
    [HttpGet("/users/by-city")]
    public IActionResult GetUsersByCity(
        [Description("The city name")] string city,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetByCity(city);
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get users by state")]
    [HttpGet("/users/by-state")]
    public IActionResult GetUsersByState(
        [Description("The state name or code")] string state,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetByState(state);
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get top cities by user count")]
    [HttpGet("/users/top-cities")]
    public IActionResult GetTopCities(
        [Description("Number of top cities to return")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var topCities = svc.GetTopCities(top);
        return Ok(topCities);
    }

    [McpServerTool]
    [Description("Get top businesses by user count")]
    [HttpGet("/users/top-businesses")]
    public IActionResult GetTopBusinesses(
        [Description("Number of top businesses to return")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var topBusinesses = svc.GetTopBusinesses(top);
        return Ok(topBusinesses);
    }

    [McpServerTool]
    [Description("Get total users by city")]
    [HttpGet("/users/count-by-city")]
    public string GetTotalUsersByCity(
        [Description("The city name")] string city,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return $"Error: {svc.ErrorLoadData}";

        int count = svc.GetTotalUsersByCity(city);
        return $"Total users in {city}: {count}";
    }

    [McpServerTool]
    [Description("Get all business names")]
    [HttpGet("/users/business-names")]
    public IActionResult GetAllBusinessNames(
        [Description("Sort by name (true) or by count (false)")] bool sortByName = true,
        [Description("Sort in descending order")] bool descending = false,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var businesses = svc.GetAllBusinessNames(sortByName, descending);
        return Ok(businesses);
    }

    [McpServerTool]
    [Description("Get users by NMLS ID")]
    [HttpGet("/users/by-nmls")]
    public IActionResult GetUsersByNMLSID(
        [Description("The NMLS ID to search for")] string nmlsId,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetByNMLSID(nmlsId);
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get users by licensing entity")]
    [HttpGet("/users/by-licensing-entity")]
    public IActionResult GetUsersByLicensingEntity(
        [Description("The licensing entity name")] string entity,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetByLicensingEntity(entity);
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get users by company name")]
    [HttpGet("/users/by-company")]
    public IActionResult GetUsersByCompany(
        [Description("The company name to search for")] string companyName,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetByCompany(companyName);
        return Ok(users);
    }

    [McpServerTool]
    [Description("Get user statistics (total users, users with NMLS, users with license)")]
    [HttpGet("/users/stats")]
    public string GetUserStatistics(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return $"Error: {svc.ErrorLoadData}";

        var (totalUsers, withNMLS, withLicense) = svc.GetUserStats();
        return $"Total Users: {totalUsers}, With NMLS: {withNMLS}, With License: {withLicense}";
    }

    [McpServerTool]
    [Description("Get users added within a date range")]
    [HttpGet("/users/by-date-range")]
    public IActionResult GetUsersByDateRange(
        [Description("Start date (optional)")] DateTime? from = null,
        [Description("End date (optional)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
            return BadRequest($"Error: {svc.ErrorLoadData}");

        var users = svc.GetUsersByDateRange(from, to);
        return Ok(users);
    }
}