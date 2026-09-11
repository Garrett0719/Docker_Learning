using Microsoft.AspNetCore.Diagnostics.HealthChecks;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
var app = builder.Build();

app.MapGet("/", () => "Docker Security Lab!");

app.MapGet("/config-check", (IConfiguration configuration) =>
{
    string? message = configuration["App:Message"];
    string? apiKey = configuration["Secrets:ApiKey"];
    return Results.Ok(new
    {
        Message = message,
        SecretLoaded = !string.IsNullOrWhiteSpace(apiKey)
    });
});

app.MapHealthChecks(
    "/health/startup",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/ready");

var startupDelaySeconds = int.TryParse(
    Environment.GetEnvironmentVariable("STARTUP_DELAY_SECONDS"),
    out var seconds)
    ? seconds
    : 0;
if(startupDelaySeconds > 0)
{
    Console.WriteLine(
        $"Start up delayed by {startupDelaySeconds} seconds ...");
    await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds));
}

app.Run();
