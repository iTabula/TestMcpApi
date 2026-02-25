using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Net.Http.Headers;
using System.Text.Json.Serialization;
using KamInfrastructure.Interfaces;
using KamHttp.Services;
using KamHttp.Interfaces;
using KamHttp.Helpers;
using KamWebBasic.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Get the base URL from configuration
var webApiBaseUrl = builder.Configuration.GetValue<string>("WebApi:BaseUrl");

builder.Services.AddHttpClient("MyWebApi",
        client =>
        {
            client.BaseAddress = new Uri(webApiBaseUrl);
            client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, "KAM.Web");
            client.Timeout = TimeSpan.FromSeconds(120);
        });

builder.Services.AddSingleton<IFactoryHttpClient, FactoryHttpClient>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
    });

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
});

builder.Services.AddHttpContextAccessor();

// Configure options
builder.Services.Configure<VapiConfiguration>(builder.Configuration.GetSection("Vapi"));
builder.Services.Configure<McpConfiguration>(builder.Configuration.GetSection("Mcp"));

// Register McpSseClient as singleton (one instance for the entire application)
builder.Services.AddSingleton<McpSseClient>(sp =>
{
    var mcpConfig = builder.Configuration.GetSection("Mcp").Get<McpConfiguration>();
    var vapiConfig = builder.Configuration.GetSection("Vapi").Get<VapiConfiguration>();
    var logger = sp.GetRequiredService<ILogger<McpSseClient>>();

    var client = new McpSseClient(mcpConfig?.SseEndpoint ?? "https://freemypalestine.com/api/mcp/sse", logger);

    if (vapiConfig != null)
    {
        client.SetVapiClient(vapiConfig.PrivateApiKey, vapiConfig.AssistantId);
    }

    return client;
});

var app = builder.Build();

// Initialize MCP client on startup
var mcpClient = app.Services.GetRequiredService<McpSseClient>();
await mcpClient.ConnectAsync();
await mcpClient.InitializeAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
