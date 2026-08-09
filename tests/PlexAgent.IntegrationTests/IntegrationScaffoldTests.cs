namespace PlexAgent.IntegrationTests;

public class IntegrationScaffoldTests
{
    [Fact(Skip = "Integration tests require API keys; enable when PLEXAGENT_*_API_KEY env vars are set.")]
    public void Placeholder_SkippedWithoutKeys()
    {
        Assert.True(true);
    }
}
