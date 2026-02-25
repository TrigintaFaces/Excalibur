using System.Text.Json;

using Excalibur.EventSourcing.Upcasting;

namespace Excalibur.EventSourcing.Tests.Upcasting;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonEventTransformerShould
{
	[Fact]
	public void ThrowWhenEventTypeIsNullOrEmpty()
	{
		var rules = new JsonTransformRuleBuilder().Build();
		Should.Throw<ArgumentException>(() => new JsonEventTransformer(null!, 1, 2, rules));
		Should.Throw<ArgumentException>(() => new JsonEventTransformer("", 1, 2, rules));
	}

	[Fact]
	public void ThrowWhenRulesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new JsonEventTransformer("Test", 1, 2, null!));
	}

	[Fact]
	public void ExposePropertiesCorrectly()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var transformer = new JsonEventTransformer("OrderCreated", 1, 2, rules);

		// Assert
		transformer.EventType.ShouldBe("OrderCreated");
		transformer.FromVersion.ShouldBe(1);
		transformer.ToVersion.ShouldBe(2);
	}

	[Fact]
	public void CanUpgradeMatchingEventTypeAndVersion()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var transformer = new JsonEventTransformer("OrderCreated", 1, 2, rules);

		// Act & Assert
		transformer.CanUpgrade("OrderCreated", 1).ShouldBeTrue();
		transformer.CanUpgrade("OrderCreated", 2).ShouldBeFalse();
		transformer.CanUpgrade("Other", 1).ShouldBeFalse();
	}

	[Fact]
	public void ApplyRenameRule()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Rename("OldName", "NewName")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"OldName": "value"}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("NewName", out var prop).ShouldBeTrue();
		prop.GetString().ShouldBe("value");
		doc.RootElement.TryGetProperty("OldName", out _).ShouldBeFalse();
	}

	[Fact]
	public void ApplyRemoveRule()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Remove("Deprecated")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Deprecated": "old", "Keep": "value"}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Deprecated", out _).ShouldBeFalse();
		doc.RootElement.TryGetProperty("Keep", out var keepProp).ShouldBeTrue();
		keepProp.GetString().ShouldBe("value");
	}

	[Fact]
	public void ApplyAddDefaultRuleWhenPropertyMissing()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("NewProp", 42)
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Existing": "value"}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("NewProp", out var prop).ShouldBeTrue();
		prop.GetInt32().ShouldBe(42);
	}

	[Fact]
	public void NotOverwriteExistingPropertyWithAddDefault()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("Existing", 99)
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Existing": 42}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.GetProperty("Existing").GetInt32().ShouldBe(42);
	}

	[Fact]
	public void ApplyMoveRule()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Move("Source", "Target")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Source": "value"}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Source", out _).ShouldBeFalse();
		doc.RootElement.TryGetProperty("Target", out var prop).ShouldBeTrue();
		prop.GetString().ShouldBe("value");
	}

	[Fact]
	public void HandleByteArrayInput()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("Added", "hello")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var bytes = System.Text.Encoding.UTF8.GetBytes("""{"Existing": 1}""");

		// Act
		var result = (string)transformer.Upgrade(bytes);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Added", out _).ShouldBeTrue();
	}

	[Fact]
	public void HandleJsonElementInput()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("Added", true)
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		using var doc = JsonDocument.Parse("""{"Existing": 1}""");
		var element = doc.RootElement;

		// Act
		var result = (string)transformer.Upgrade(element);

		// Assert
		using var resultDoc = JsonDocument.Parse(result);
		resultDoc.RootElement.TryGetProperty("Added", out var prop).ShouldBeTrue();
		prop.GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public void ThrowForUnsupportedInputType()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);

		// Act & Assert
		Should.Throw<ArgumentException>(() => transformer.Upgrade(42));
	}

	[Fact]
	public void ThrowWhenOldEventIsNull()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => transformer.Upgrade(null!));
	}

	[Fact]
	public void ThrowWhenJsonIsNotAnObject()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);

		// Act & Assert - JSON array is not an object
		Should.Throw<InvalidOperationException>(() => transformer.Upgrade("[1,2,3]"));
	}

	[Fact]
	public void ApplyMultipleRulesInSequence()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Rename("OldField", "RenamedField")
			.Remove("Deprecated")
			.AddDefault("NewField", "default")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"OldField": "val", "Deprecated": true, "Keep": 1}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("RenamedField", out _).ShouldBeTrue();
		doc.RootElement.TryGetProperty("OldField", out _).ShouldBeFalse();
		doc.RootElement.TryGetProperty("Deprecated", out _).ShouldBeFalse();
		doc.RootElement.TryGetProperty("NewField", out _).ShouldBeTrue();
		doc.RootElement.TryGetProperty("Keep", out _).ShouldBeTrue();
	}

	[Fact]
	public void HandleAddDefaultWithNullValue()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("NullProp", null)
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Existing": 1}""";

		// Act
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("NullProp", out var prop).ShouldBeTrue();
		prop.ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public void HandleRenameWhenSourcePropertyDoesNotExist()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Rename("NonExistent", "Target")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Other": 1}""";

		// Act - should not throw
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Target", out _).ShouldBeFalse();
		doc.RootElement.TryGetProperty("Other", out _).ShouldBeTrue();
	}

	[Fact]
	public void HandleMoveWhenSourcePropertyDoesNotExist()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Move("NonExistent", "Target")
			.Build();
		var transformer = new JsonEventTransformer("Test", 1, 2, rules);
		var json = """{"Other": 1}""";

		// Act - should not throw
		var result = (string)transformer.Upgrade(json);

		// Assert
		using var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Target", out _).ShouldBeFalse();
	}
}
