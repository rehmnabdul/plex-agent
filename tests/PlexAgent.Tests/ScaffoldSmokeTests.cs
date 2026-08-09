using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlexAgent.Configuration;
using PlexAgent.DependencyInjection;
using PlexAgent.Providers.OpenAI;

namespace PlexAgent.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void AddPlexAgent_WithConfiguration_RegistersBuilder()
    {
        const string json = """
            {
              "PlexAgent": {
                "DefaultAgent": "SupportAgent",
                "Agents": {
                  "SupportAgent": {
                    "Provider": "OpenAI",
                    "Model": "gpt-4o-mini"
                  }
                }
              }
            }
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        var services = new ServiceCollection();
        var builder = services.AddPlexAgent(configuration).AddOpenAI();

        Assert.NotNull(builder);
        Assert.Same(services, builder.Services);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlexAgentOptions>>().Value;

        Assert.Equal("SupportAgent", options.DefaultAgent);
        Assert.True(options.Agents.ContainsKey("SupportAgent"));
        Assert.Equal("OpenAI", options.Agents["SupportAgent"].Provider);
        Assert.Equal("gpt-4o-mini", options.Agents["SupportAgent"].Model);
    }

    [Fact]
    public void AddPlexAgent_WithCallback_BindsOptions()
    {
        var services = new ServiceCollection();
        services.AddPlexAgent(options =>
        {
            options.DefaultAgent = "Summarizer";
            options.ToolLoop.MaxIterations = 5;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlexAgentOptions>>().Value;

        Assert.Equal("Summarizer", options.DefaultAgent);
        Assert.Equal(5, options.ToolLoop.MaxIterations);
    }
}
