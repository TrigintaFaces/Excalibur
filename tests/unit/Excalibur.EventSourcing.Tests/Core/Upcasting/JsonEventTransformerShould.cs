// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.Upcasting;

namespace Excalibur.EventSourcing.Tests.Core.Upcasting;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class JsonEventTransformerShould
{
	[Fact]
	public void ThrowWhenEventTypeIsEmpty()
	{
		Should.Throw<ArgumentException>(() =>
			new JsonEventTransformer("", 1, 2, []));
	}

	[Fact]
	public void ThrowWhenRulesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new JsonEventTransformer("MyEvent", 1, 2, null!));
	}

	[Fact]
	public void ExposeProperties()
	{
		// Arrange & Act
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		// Assert
		sut.EventType.ShouldBe("MyEvent");
		sut.FromVersion.ShouldBe(1);
		sut.ToVersion.ShouldBe(2);
	}

	[Fact]
	public void ReturnTrueForCanUpgradeWhenMatches()
	{
		// Arrange
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		// Act & Assert
		sut.CanUpgrade("MyEvent", 1).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForCanUpgradeWhenTypeDoesNotMatch()
	{
		// Arrange
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		// Act & Assert
		sut.CanUpgrade("OtherEvent", 1).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForCanUpgradeWhenVersionDoesNotMatch()
	{
		// Arrange
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		// Act & Assert
		sut.CanUpgrade("MyEvent", 2).ShouldBeFalse();
	}

	[Fact]
	public void RenameProperty()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Rename("OldName", "NewName")
			.Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = """{"OldName":"value"}""";

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("NewName", out var prop).ShouldBeTrue();
		prop.GetString().ShouldBe("value");
		doc.RootElement.TryGetProperty("OldName", out _).ShouldBeFalse();
	}

	[Fact]
	public void RemoveProperty()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Remove("Deprecated")
			.Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = """{"Deprecated":"old","Keep":"val"}""";

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Deprecated", out _).ShouldBeFalse();
		doc.RootElement.TryGetProperty("Keep", out _).ShouldBeTrue();
	}

	[Fact]
	public void AddDefaultProperty()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("NewField", 42)
			.Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = """{"Existing":"val"}""";

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("NewField", out var prop).ShouldBeTrue();
		prop.GetInt32().ShouldBe(42);
	}

	[Fact]
	public void NotOverwriteExistingPropertyWithDefault()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("Field", 42)
			.Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = """{"Field":99}""";

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		var doc = JsonDocument.Parse(result);
		doc.RootElement.GetProperty("Field").GetInt32().ShouldBe(99);
	}

	[Fact]
	public void MoveProperty()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Move("Source", "Target")
			.Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = """{"Source":"moved"}""";

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("Target", out var prop).ShouldBeTrue();
		prop.GetString().ShouldBe("moved");
		doc.RootElement.TryGetProperty("Source", out _).ShouldBeFalse();
	}

	[Fact]
	public void ApplyMultipleRulesInOrder()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder()
			.Rename("A", "B")
			.AddDefault("C", "added")
			.Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = """{"A":"val"}""";

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		var doc = JsonDocument.Parse(result);
		doc.RootElement.TryGetProperty("B", out _).ShouldBeTrue();
		doc.RootElement.TryGetProperty("C", out _).ShouldBeTrue();
	}

	[Fact]
	public void AcceptByteArrayInput()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var input = System.Text.Encoding.UTF8.GetBytes("""{"Key":"value"}""");

		// Act
		var result = (string)sut.Upgrade(input);

		// Assert
		result.ShouldContain("Key");
	}

	[Fact]
	public void AcceptJsonElementInput()
	{
		// Arrange
		var rules = new JsonTransformRuleBuilder().Build();
		var sut = new JsonEventTransformer("MyEvent", 1, 2, rules);
		var doc = JsonDocument.Parse("""{"Key":"value"}""");

		// Act
		var result = (string)sut.Upgrade(doc.RootElement);

		// Assert
		result.ShouldContain("Key");
	}

	[Fact]
	public void ThrowWhenOldEventIsNull()
	{
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		Should.Throw<ArgumentNullException>(() => sut.Upgrade(null!));
	}

	[Fact]
	public void ThrowWhenEventDataIsNotJsonObject()
	{
		// Arrange
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => sut.Upgrade("42"));
	}

	[Fact]
	public void ThrowWhenInputTypeIsUnsupported()
	{
		// Arrange
		var sut = new JsonEventTransformer("MyEvent", 1, 2, []);

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.Upgrade(12345));
	}
}
