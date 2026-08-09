using System.Text.Json;
using PlexAgent.Abstractions;
using PlexAgent.DependencyInjection;
using PlexAgent.Models;
using PlexAgent.Providers.OpenAI;
using PlexAgent.Tools;

var builder = WebApplication.CreateBuilder(args);

var lookup = ToolDefinition.Create(
    "lookup_account",
    "Looks up a demo account by id",
    JsonDocument.Parse("""{"type":"object","properties":{"accountId":{"type":"string"}},"required":["accountId"]}"""),
    (args, _) =>
    {
        var id = args.GetProperty("accountId").GetString() ?? "unknown";
        return Task.FromResult<object?>(new { accountId = id, status = "active", plan = "pro" });
    });

builder.Services
    .AddPlexAgent(builder.Configuration)
    .AddTool(lookup)
    .AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["PlexAgent:Providers:OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("PLEXAGENT_OPENAI_API_KEY")
            ?? string.Empty;
        o.DefaultModel = "gpt-4o-mini";
    });

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    endpoints = new[] { "POST /ask", "POST /ask/structured", "POST /session/demo" }
}));

app.MapPost("/ask", async (AskRequest request, IAgentFactory factory, CancellationToken cancellationToken) =>
{
    var agent = factory.GetAgent(request.Agent ?? "DemoAgent");
    var response = await agent.AskAsync(request.Prompt, cancellationToken: cancellationToken);
    return Results.Ok(new
    {
        response.Content,
        response.ProviderId,
        response.Model,
        response.FinishReason
    });
});

app.MapPost("/ask/structured", async (AskRequest request, IAgentFactory factory, CancellationToken cancellationToken) =>
{
    var agent = factory.GetAgent(request.Agent ?? "DemoAgent");
    var response = await agent.AskAsync<GreetingPlan>(request.Prompt, cancellationToken: cancellationToken);
    return Results.Ok(new
    {
        response.Data,
        response.Content,
        response.ProviderId,
        response.Model
    });
});

app.MapPost("/session/demo", async (SessionDemoRequest request, IAgentFactory factory, CancellationToken cancellationToken) =>
{
    var agent = factory.GetAgent(request.Agent ?? "DemoAgent");
    var session = agent.CreateSession();
    var first = await session.AskAsync(request.FirstPrompt, cancellationToken: cancellationToken);
    var second = await session.AskAsync(request.SecondPrompt, cancellationToken: cancellationToken);
    return Results.Ok(new
    {
        turns = session.TurnCount,
        session.LastProviderId,
        session.LastModel,
        first = first.Content,
        second = second.Content,
        historyCount = session.History.Count
    });
});

app.Run();

internal sealed record AskRequest(string Prompt, string? Agent = null);

internal sealed record SessionDemoRequest(
    string FirstPrompt,
    string SecondPrompt,
    string? Agent = null);

internal sealed class GreetingPlan
{
    public string Title { get; set; } = string.Empty;

    public int Steps { get; set; }
}
