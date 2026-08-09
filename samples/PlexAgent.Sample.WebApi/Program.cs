using PlexAgent.DependencyInjection;
using PlexAgent.Providers.OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPlexAgent(builder.Configuration)
    .AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["PlexAgent:Providers:OpenAI:ApiKey"] ?? string.Empty;
        o.DefaultModel = "gpt-4o-mini";
    });

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    message = "Plex Agent WebApi sample scaffolded. Agent endpoints arrive in later phases."
}));

app.Run();
