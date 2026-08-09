using System.Text.Json;
using PlexAgent.Exceptions;
using PlexAgent.StructuredOutput;

namespace PlexAgent.Tests;

public class JsonSchemaValidationTests
{
    [Fact]
    public void ValidateStructured_RejectsWrongTypeAndAdditionalProperties()
    {
        var schema = JsonDocument.Parse(
            """
            {
              "type":"object",
              "properties":{
                "title":{"type":"string","minLength":1},
                "steps":{"type":"integer","minimum":1}
              },
              "required":["title","steps"],
              "additionalProperties":false
            }
            """);

        var ex = Assert.Throws<StructuredOutputException>(() =>
            JsonSchemaValidator.ValidateStructured(schema, """{"title":"A","steps":2,"extra":true}""", "Plan"));
        Assert.Contains("additional property", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateStructured_RejectsEnumMismatchAndBounds()
    {
        var schema = JsonDocument.Parse(
            """
            {
              "type":"object",
              "properties":{
                "status":{"type":"string","enum":["active","closed"]},
                "tags":{"type":"array","minItems":1,"items":{"type":"string"}}
              },
              "required":["status","tags"]
            }
            """);

        Assert.Throws<StructuredOutputException>(() =>
            JsonSchemaValidator.ValidateStructured(schema, """{"status":"pending","tags":["a"]}""", "Plan"));

        Assert.Throws<StructuredOutputException>(() =>
            JsonSchemaValidator.ValidateStructured(schema, """{"status":"active","tags":[]}""", "Plan"));
    }

    [Fact]
    public void ValidateToolArguments_RejectsWrongPropertyType()
    {
        var schema = JsonDocument.Parse(
            """
            {
              "type":"object",
              "properties":{"accountId":{"type":"string"}},
              "required":["accountId"],
              "additionalProperties":false
            }
            """);

        using var args = JsonDocument.Parse("""{"accountId":123}""");
        var ex = Assert.Throws<ToolExecutionException>(() =>
            JsonSchemaValidator.ValidateToolArguments(schema, args.RootElement, "lookup_account"));
        Assert.Equal("lookup_account", ex.ToolName);
        Assert.Contains("type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
