using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Numerics;
using System.Text.Json;
using TestMcpApi.Helpers;
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
    [Description("Look up a customer's record using their phone number.")]
    [HttpGet("/users/customer_phone")]
    public async Task<string> GetCustomerDetails(
    [Description("The customer's phone number in E.164 format.")]
        string phoneNumber)
    {
        //Lookup the agent's info based on the phone number
        var users = await svc.GetByPhone(phoneNumber);

        if (users == null)
        {
            return "We could not find a user with that phone number.";
        }

        // Vapi will have replaced {{customer.number}} with "+1234567890" before this is called
        string phone = phoneNumber.Replace(",", "").Replace("-", "").Replace(" ", "");
        phone = Common.FormatPhoneNumber(phone);
        return $"you have been authenticated using your phone number {phone}. Welcome back {users.Name}";
    }

    [McpServerTool]
    [Description("Get all users")]
    [HttpGet("/users/all")]
    public async Task<string> GetAllUsers(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = await svc.GetUsers();
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get user by ID")]
    [HttpGet("/users/by-id")]
    public async Task<string> GetUserById(
        [Description("The user ID to look up")] int userId,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var user = await svc.GetUserById(userId);
        if (user == null)
            return $"User with ID {userId} not found.";

        return JsonSerializer.Serialize(user);
    }

    [McpServerTool]
    [Description("Search users by name (full name, first name, or last name)")]
    [HttpGet("/users/by-name")]
    public string GetUsersByName(
        [Description("The name to search for")] string name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetByName(name);
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get user by email address")]
    [HttpGet("/users/by-email")]
    public string GetUserByEmail(
        [Description("The email address to look up")] string email,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var user = svc.GetByEmail(email);
        if (user == null)
            return $"User with email {email} not found.";

        return JsonSerializer.Serialize(user);
    }

    [McpServerTool]
    [Description("Get users by city")]
    [HttpGet("/users/by-city")]
    public string GetUsersByCity(
        [Description("The city name")] string city,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetByCity(city);
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get users by state")]
    [HttpGet("/users/by-state")]
    public string GetUsersByState(
        [Description("The state name or code")] string state,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetByState(state);
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get top cities by user count")]
    [HttpGet("/users/top-cities")]
    public string GetTopCities(
        [Description("Number of top cities to return")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var topCities = svc.GetTopCities(top);
        return JsonSerializer.Serialize(topCities);
    }

    [McpServerTool]
    [Description("Get top businesses by user count")]
    [HttpGet("/users/top-businesses")]
    public string GetTopBusinesses(
        [Description("Number of top businesses to return")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var topBusinesses = svc.GetTopBusinesses(top);
        return JsonSerializer.Serialize(topBusinesses);
    }

    [McpServerTool]
    [Description("Get total users by city")]
    [HttpGet("/users/count-by-city")]
    public string GetTotalUsersByCity(
        [Description("The city name")] string city,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        int count = svc.GetTotalUsersByCity(city);
        return $"Total users in {city}: {count}";
    }

    [McpServerTool]
    [Description("Get all business names")]
    [HttpGet("/users/business-names")]
    public string GetAllBusinessNames(
        [Description("Sort by name (true) or by count (false)")] bool sortByName = true,
        [Description("Sort in descending order")] bool descending = false,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var businesses = svc.GetAllBusinessNames(sortByName, descending);
        return JsonSerializer.Serialize(businesses);
    }

    [McpServerTool]
    [Description("Get users by NMLS ID")]
    [HttpGet("/users/by-nmls")]
    public string GetUsersByNMLSID(
        [Description("The NMLS ID to search for")] string nmlsId,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetByNMLSID(nmlsId);
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get users by licensing entity")]
    [HttpGet("/users/by-licensing-entity")]
    public string GetUsersByLicensingEntity(
        [Description("The licensing entity name")] string entity,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetByLicensingEntity(entity);
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get users by company name")]
    [HttpGet("/users/by-company")]
    public string GetUsersByCompany(
        [Description("The company name to search for")] string companyName,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetByCompany(companyName);
        return JsonSerializer.Serialize(users);
    }

    [McpServerTool]
    [Description("Get user statistics (total users, users with NMLS, users with license)")]
    [HttpGet("/users/stats")]
    public string GetUserStatistics(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var (totalUsers, withNMLS, withLicense) = svc.GetUserStats();
        return $"Total Users: {totalUsers}, With NMLS: {withNMLS}, With License: {withLicense}";
    }

    [McpServerTool]
    [Description("Get users added within a date range")]
    [HttpGet("/users/by-date-range")]
    public string GetUsersByDateRange(
        [Description("Start date (optional)")] DateTime? from = null,
        [Description("End date (optional)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown")
    {
        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadData))
        {
            return "not available right now";
        }

        var users = svc.GetUsersByDateRange(from, to);
        return JsonSerializer.Serialize(users);
    }
}