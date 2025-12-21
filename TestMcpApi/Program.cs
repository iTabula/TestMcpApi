using ModelContextProtocol.AspNetCore;
using TestMcpApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// 👇 Add MCP Server to IoC and automatically discover tools from this assembly
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(); // Scans your assembly for decorated classes and methods

builder.Services.AddSingleton<ILoanTransactionService, LoanTransactionService>();
builder.Services.AddSingleton<IRealTransactionService, RealTransactionService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

//Map Mcp endpoints
app.MapMcp(pattern: "api/mcp"); // Exposes endpoints like /api/mcp/sse and /api/mcp/messages

app.Run();
