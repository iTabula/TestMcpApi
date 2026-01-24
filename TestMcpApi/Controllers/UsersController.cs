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
    public static string GetCustomerDetails(
        [Description("The customer's phone number in E.164 format.")]
        string phoneNumber)
    {
        //Clean up the phone number
        if (phoneNumber.Length > 10)
        {
            phoneNumber = phoneNumber.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "");
        }
        if (phoneNumber.Length > 10 && phoneNumber.StartsWith("+1"))
        {
            phoneNumber = phoneNumber.Replace("+1", "");
        }
        phoneNumber = phoneNumber.Trim();

        //Lookup the agent's info based on the phone number
        var users = new UserService().GetByPhone(phoneNumber);

        if (users == null)
        {
            return $"The customer's phone number {phoneNumber} is not found";
        }

        // Vapi will have replaced {{customer.number}} with "+1234567890" before this is called
        string phone = Common.FormatPhoneNumber(phoneNumber);
        return $"you have been authenticated using your phone number {phone}. Welcome back {users.FirstName}";
    }
}