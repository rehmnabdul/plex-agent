using PlexAgent.Models;
using PlexAgent.Providers.OpenAI;

namespace PlexAgent.Providers.OpenAI.Tests;

public class OpenAiScaffoldTests
{
    [Fact]
    public void ProviderId_Constant_IsOpenAI()
    {
        Assert.Equal("OpenAI", LlmProviderIds.OpenAI);
        Assert.Equal("PlexAgent.Providers.OpenAI", typeof(OpenAiOptions).Namespace);
    }
}
