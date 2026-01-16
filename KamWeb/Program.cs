using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Net.Http.Headers;
using System.Text.Json.Serialization;
using WebApi.Interfaces;
using WebApi.Services;
using KamWeb.Helpers;
using KamWeb.Models;
using KamWeb.Services;

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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    // Cookie settings
                    options.Cookie.HttpOnly = true;
                    options.LoginPath = "/Index";
                    options.LogoutPath = "/Logout";
                    options.AccessDeniedPath = "/Index";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                });

builder.Services.AddAuthorization(options =>
{
    //Get this form the database in case we added a new one
    foreach (string feature in new List<string>() {
    "ACCESS_FEATURES", "ACCESS_USER_GROUPS", "ACCESS_USERS", "ACCESS_REQUESTS", "ACCESS_WORKORDERS",
    "ACCESS_SCAN_TO_ORDER", "ACCESS_SCAN_TO_PICKUP", "ACCESS_UPLOAD_PARTS", "ACCESS_UPLOAD_WORKORDERS",
    "ACCESS_REQUESTS_TOTALS", "ACCESS_NOTIFICATIONS"})
    {
        options.AddPolicy(feature, policy => policy.RequireClaim(feature, "true"));
    }
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
    
    var client = new McpSseClient(mcpConfig?.SseEndpoint ?? "https://mcp.kamfr.com/api/mcp/sse");
    
    if (vapiConfig != null)
    {
        client.SetVapiClient(vapiConfig.PrivateApiKey, vapiConfig.AssistantId);
    }
    
    return client;
});

// Register background service to initialize MCP client
builder.Services.AddHostedService<McpInitializationService>();

var app = builder.Build();

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
