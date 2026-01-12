using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.AspNetCore;
using System.Text.Json.Serialization;
using TestMcpApi.DBContexts;
using TestMcpApi.Interfaces;
using TestMcpApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.Development.json", optional: true)
.Build();


// Add services to the container.
//builder.Services.AddControllers();

builder.Services.AddControllers()
    .AddJsonOptions(o => { o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve; });

// 👇 Add MCP Server to IoC and automatically discover tools from this assembly
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(); // Scans your assembly for decorated classes and methods

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "MyAllowSpecificOrigins",
                      builder =>
                      {
                          //builder.WithOrigins("http://example.com",
                          //"http://localhost:3000") // Specify allowed origins
                          builder.AllowAnyOrigin()
                               .AllowAnyHeader()
                               .AllowAnyMethod();
                      });
});

//builder.Services.AddSingleton<IFactoryHttpClient, FactoryHttpClient>();
//builder.Services.AddSingleton<ILoanTransactionService, LoanTransactionService>();
//builder.Services.AddSingleton<IRealTransactionService, RealTransactionService>();
//builder.Services.AddSingleton<ILenderService, LenderService>();
//builder.Services.AddSingleton<IThirdPartyService, ThirdPartyService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<KamfrContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
//builder.Services.AddScoped<DbContext, IrvineInventContext>();  // Ensure this is registered
builder.Services.AddMemoryCache();

//builder.Services.AddSingleton<IrvineInventContext>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();

//adding required services for auth
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = true;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = configuration["JwtOptions:Issuer"]!,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["JwtOptions:SigningKey"]!)),
        ValidAudience = configuration["JwtOptions:Audience"]!,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}).AddCookie(options =>
{
    options.LoginPath = "/Index";
    options.LogoutPath = "/Logout";
    //options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
});

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KAM", Version = "v1" });
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "JWT Authentication",
        Description = "Enter JWT Bearer token **_only_**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer", // must be lower case
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {securityScheme, new string[] { }}
                });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

//Map Mcp endpoints
app.MapMcp(pattern: "api/mcp"); // Exposes endpoints like /api/mcp/sse and /api/mcp/messages

app.UseCors("MyAllowSpecificOrigins"); // Apply the named policy

app.Run();
