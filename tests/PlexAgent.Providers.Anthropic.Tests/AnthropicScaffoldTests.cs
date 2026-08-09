using PlexAgent.Models;
using PlexAgent.Providers.Anthropic;

namespace PlexAgent.Providers.Anthropic.Tests;

public class AnthropicScaffoldTests
{
    [Fact]
    public void ProviderId_Constant_IsAnthropic()
    {
        Assert.Equal("Anthropic", LlmProviderIds.Anthropic);
        Assert.Equal("PlexAgent.Providers.Anthropic", typeof(AnthropicOptions).Namespace);
    }
}
