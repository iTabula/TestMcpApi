using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Phonix;
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
        var users = svc.GetByPhone(phoneNumber);
        if (users == null)
        {
            return $"The customer's phone number {phoneNumber} is not found";
        }

        var context = _httpContextAccessor.HttpContext;

        //// Extract the Call ID sent by Vapi
        bool AddToVapiCalls = false;
        if (context != null && context.Request.Headers.TryGetValue("X-Call-Id", out var callId))
        {
            AddToVapiCalls = await svc.AddCallToVapiCallsAsync(call: new VapiCall
            {
                CallId = callId,
                UserId = users.UserID,
                UserRole = users.Role,
                Phone = phoneNumber,
                CreatedOn = DateTime.UtcNow,
                LastUpdatedOn = DateTime.UtcNow,
                IsAuthenticated = 1
            });
        }

        // Vapi will have replaced {{customer.number}} with "+1234567890" before this is called
        string phone = Common.FormatPhoneNumber(phoneNumber);
        string IsAuthenticatedText = AddToVapiCalls ? "have been authenticated " : "could not be authenticated";
        string Welcome = AddToVapiCalls ? $" Welcome back {users.FirstName}!!" : "";
        return $"You {IsAuthenticatedText} using your phone number {phone}.{Welcome}";
    }

    [McpServerTool]
    [Description("What's phone number for agent?")]
    [HttpGet("/users/agent-phone")]
    public async Task<string> GetAgentPhoneNumber(
    [Description("the agent name")] string agent_name = "unknown",
    [Description("user_id")] int user_id = 0,
    [Description("user_role")] string user_role = "unknown",
    [Description("token")] string token = "unknown")
    {
        //Step 1: Get the data First
        // Proceed with the tool execution for Admin users
        var data = await svc.GetUsers();

        if (data == null || data.Count() == 0)
            return "There are no agent available for this name.";

        //Try to find the name based on input
        var doubleMetaphone = new DoubleMetaphone();
        string searchKey = doubleMetaphone.BuildKey(agent_name);

        // 2. Perform the search
        var result = data
            .OrderBy(x => {
                // Generate the phonetic key for each item in the list
                string itemKey = doubleMetaphone.BuildKey(x.Name);

                // Calculate distance between the phonetic keys
                // (Closer phonetic keys = smaller distance)
                return Common.CalculateLevenshteinDistance(searchKey, itemKey);
            })
            .FirstOrDefault();

        if (result == null)
        {
            var searchCode = Common.GetSoundex(agent_name); // Implementation from previous example

            result = data
                .Where(x => Common.GetSoundex(x.Name) == searchCode) // Filter for identical sounds
                .OrderByDescending(x => Common.CalculateSoundexDifference(searchCode, Common.GetSoundex(x.Name)))
                .FirstOrDefault();
        }

        if (result == null)
        {
            result = data
                .OrderBy(x => Common.CalculateLevenshteinDistance(agent_name, x.Name))
                .ThenByDescending(x => x.UserID) // Optional: Tie-breaker using the highest count
                .FirstOrDefault();
        }

        if (result == null)
            return "I could not find an agent with this name.";

        // Check if call coming form Web/Mobile app then you should have all three parameters with correct values because user logged in
        if (user_id != 0 && user_role != "unknown" && token != "unknown")
        {
            if (!user_role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return "Access denied. Only users with Admin role can access this information.";
            }
        }
        else
        {
            //Coming here from a live call to VAPI phone number
            //Get the call details from HTTP Headers Context
            var context = _httpContextAccessor.HttpContext;

            //// Extract the Call ID sent by Vapi
            if (context != null && context.Request.Headers.TryGetValue("X-Call-Id", out var callId))
            {
                //Coming here from a live call to VAPI phone number
                VapiCall vapiCall = new UserService().GetCurrentVapiCallAsync(CallId: callId).Result;
                if (vapiCall != null)
                {
                    if (vapiCall.IsAuthenticated == 0)
                    {
                        return "Access denied. You are not authenticated yet!";
                    }

                    if (vapiCall.UserId != int.Parse(result.UserID.ToString()) && vapiCall.UserRole.ToLower().Trim() != "admin")
                    {
                        return "Access denied. you do not have permissions to lookup transactions for another Agent!";
                    }
                }
                else
                {
                    return "Access denied. Call details not found!";
                }
            }
            else
            {
                return "Access denied. Call ID not found in request headers!";
            }
        }
        string phone = Common.FormatPhoneNumber(result.Phone);
        return $"{result.Name} phone number {phone} and email {result.Email}";
    }

    [McpServerTool]
    [Description("Use this tool when no other tool matches the user's request. " +
    "Handles general questions, clarification requests, or unrecognized queries. " +
    "Provides helpful guidance to the user.")]
    [HttpGet("/users/help")]
    public string HandleUnmatchedQuery(
    [Description("The user's original question or request")] string query)
    {
        return $"I'm not sure how to help with '{query}'. " +
               "I can help you with: " +
               "- Agent transactions and statistics " +
               "- Loan information and analytics " +
               "- Top agents and performance data " +
               "- Escrow and title company information " +
               "- Property details and loan statistics. " +
               "Could you please rephrase your question?";
    }

}