using PlexAgent.Models;
using PlexAgent.Providers.Gemini;

namespace PlexAgent.Providers.Gemini.Tests;

public class GeminiScaffoldTests
{
    [Fact]
    public void ProviderId_Constant_IsGemini()
    {
        Assert.Equal("Gemini", LlmProviderIds.Gemini);
        Assert.Equal("PlexAgent.Providers.Gemini", typeof(GeminiOptions).Namespace);
    }
}
