using AIConsumptionTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddSingleton<WebDatabaseService>();
builder.Services.AddSingleton<AgentProcessService>();

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Content Security Policy
var isDevelopment = app.Environment.IsDevelopment();
app.Use(async (context, next) =>
{
    if (isDevelopment)
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://unpkg.com; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self' ws: wss:;");
    }
    else
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' https://unpkg.com https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://unpkg.com; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self';");
    }
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// API endpoints for agent management
app.MapGet("/api/agent/status", async (AgentProcessService agentService) =>
{
    var (isRunning, port) = await agentService.GetAgentStatusAsync();
    return Results.Ok(new { isRunning, port });
});

app.MapPost("/api/agent/start", async (AgentProcessService agentService) =>
{
    var success = await agentService.StartAgentAsync();
    return success 
        ? Results.Ok(new { message = "Agent started" }) 
        : Results.BadRequest(new { message = "Failed to start agent" });
});

app.MapRazorPages();

// Log startup information
var dbService = app.Services.GetRequiredService<WebDatabaseService>();
if (dbService.IsDatabaseAvailable())
{
    Console.WriteLine($"Web UI connected to database: {dbService.GetType().Name}");
}
else
{
    Console.WriteLine("WARNING: Agent database not found. Web UI will show empty data.");
    Console.WriteLine("Ensure the Agent has run at least once to initialize the database.");
}

app.Run();
