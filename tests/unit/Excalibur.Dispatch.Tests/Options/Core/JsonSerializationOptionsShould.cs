using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonSerializationOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new JsonSerializationOptions();

        options.JsonSerializerOptions.ShouldNotBeNull();
        options.PreserveReferences.ShouldBeFalse();
        options.MaxDepth.ShouldBe(64);
    }

    [Fact]
    public void AllowSettingJsonSerializerOptions()
    {
        var serializerOptions = new JsonSerializerOptions { WriteIndented = true };

        var options = new JsonSerializationOptions
        {
            JsonSerializerOptions = serializerOptions,
        };

        options.JsonSerializerOptions.ShouldBeSameAs(serializerOptions);
    }

    [Fact]
    public void FallbackToNewOptionsWhenSetToNull()
    {
        var options = new JsonSerializationOptions
        {
            JsonSerializerOptions = null!,
        };

        options.JsonSerializerOptions.ShouldNotBeNull();
    }

    [Fact]
    public void BuildJsonSerializerOptionsWithMaxDepth()
    {
        var options = new JsonSerializationOptions
        {
            MaxDepth = 32,
        };

        var built = options.BuildJsonSerializerOptions();

        built.MaxDepth.ShouldBe(32);
    }

    [Fact]
    public void BuildJsonSerializerOptionsWithPreserveReferences()
    {
        var options = new JsonSerializationOptions
        {
            PreserveReferences = true,
        };

        var built = options.BuildJsonSerializerOptions();

        built.ReferenceHandler.ShouldBe(ReferenceHandler.Preserve);
    }

    [Fact]
    public void BuildJsonSerializerOptionsWithoutPreserveReferences()
    {
        var options = new JsonSerializationOptions
        {
            PreserveReferences = false,
        };

        var built = options.BuildJsonSerializerOptions();

        built.ReferenceHandler.ShouldBeNull();
    }

    [Fact]
    public void ReturnSameInstanceFromBuild()
    {
        var serializerOptions = new JsonSerializerOptions();
        var options = new JsonSerializationOptions
        {
            JsonSerializerOptions = serializerOptions,
        };

        var built = options.BuildJsonSerializerOptions();

        built.ShouldBeSameAs(serializerOptions);
    }
}
