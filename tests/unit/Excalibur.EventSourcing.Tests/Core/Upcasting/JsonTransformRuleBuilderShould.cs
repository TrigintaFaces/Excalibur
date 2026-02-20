// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Upcasting;

namespace Excalibur.EventSourcing.Tests.Core.Upcasting;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class JsonTransformRuleBuilderShould
{
	[Fact]
	public void BuildEmptyRuleSet()
	{
		// Arrange
		var builder = new JsonTransformRuleBuilder();

		// Act
		var rules = builder.Build();

		// Assert
		rules.ShouldBeEmpty();
	}

	[Fact]
	public void AddRenameRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Rename("OldProp", "NewProp")
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Rename);
		rules[0].Path.ShouldBe("OldProp");
		rules[0].TargetPath.ShouldBe("NewProp");
	}

	[Fact]
	public void AddRemoveRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Remove("Deprecated")
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Remove);
		rules[0].Path.ShouldBe("Deprecated");
	}

	[Fact]
	public void AddDefaultRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("NewField", 42)
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.AddDefault);
		rules[0].Path.ShouldBe("NewField");
		rules[0].DefaultValue.ShouldBe(42);
	}

	[Fact]
	public void AddMoveRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Move("Nested.Value", "TopLevel")
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Move);
		rules[0].Path.ShouldBe("Nested.Value");
		rules[0].TargetPath.ShouldBe("TopLevel");
	}

	[Fact]
	public void ChainMultipleRules()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Rename("OldName", "NewName")
			.Remove("Deprecated")
			.AddDefault("Flag", true)
			.Move("Deep.Field", "Flat")
			.Build();

		// Assert
		rules.Count.ShouldBe(4);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Rename);
		rules[1].Operation.ShouldBe(JsonTransformOperation.Remove);
		rules[2].Operation.ShouldBe(JsonTransformOperation.AddDefault);
		rules[3].Operation.ShouldBe(JsonTransformOperation.Move);
	}

	[Fact]
	public void ThrowWhenRenameSourcePathIsEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Rename("", "target"));
	}

	[Fact]
	public void ThrowWhenRenameTargetPathIsEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Rename("source", ""));
	}

	[Fact]
	public void ThrowWhenRemovePathIsEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Remove(""));
	}

	[Fact]
	public void ThrowWhenAddDefaultPathIsEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.AddDefault("", 42));
	}

	[Fact]
	public void ThrowWhenMoveSourcePathIsEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Move("", "target"));
	}

	[Fact]
	public void ThrowWhenMoveTargetPathIsEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Move("source", ""));
	}

	[Fact]
	public void ReturnReadOnlyList()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Rename("A", "B")
			.Build();

		// Assert
		rules.ShouldBeAssignableTo<IReadOnlyList<JsonTransformRule>>();
	}

	[Fact]
	public void AllowNullDefaultValue()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("Nullable", null)
			.Build();

		// Assert
		rules[0].DefaultValue.ShouldBeNull();
	}
}
