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
    //private static string _callId = string.Empty;

    public UsersController(IUserService userService, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        svc = userService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
       _httpContextAccessor = httpContextAccessor;
    }
    
   
}