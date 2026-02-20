using Excalibur.EventSourcing.Upcasting;

namespace Excalibur.EventSourcing.Tests.Upcasting;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonTransformRuleBuilderShould
{
	[Fact]
	public void BuildEmptyRulesList()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder().Build();

		// Assert
		rules.ShouldNotBeNull();
		rules.Count.ShouldBe(0);
	}

	[Fact]
	public void AddRenameRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Rename("Source", "Target")
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Rename);
		rules[0].Path.ShouldBe("Source");
		rules[0].TargetPath.ShouldBe("Target");
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
	public void AddAddDefaultRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("NewProp", 42)
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.AddDefault);
		rules[0].Path.ShouldBe("NewProp");
		rules[0].DefaultValue.ShouldBe(42);
	}

	[Fact]
	public void AddMoveRule()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Move("Source", "Target")
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Move);
		rules[0].Path.ShouldBe("Source");
		rules[0].TargetPath.ShouldBe("Target");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.Rename("A", "B")
			.Remove("C")
			.AddDefault("D", "val")
			.Move("E", "F")
			.Build();

		// Assert
		rules.Count.ShouldBe(4);
		rules[0].Operation.ShouldBe(JsonTransformOperation.Rename);
		rules[1].Operation.ShouldBe(JsonTransformOperation.Remove);
		rules[2].Operation.ShouldBe(JsonTransformOperation.AddDefault);
		rules[3].Operation.ShouldBe(JsonTransformOperation.Move);
	}

	[Fact]
	public void ThrowWhenRenameSourcePathIsNullOrEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Rename(null!, "Target"));
		Should.Throw<ArgumentException>(() => builder.Rename("", "Target"));
	}

	[Fact]
	public void ThrowWhenRenameTargetPathIsNullOrEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Rename("Source", null!));
		Should.Throw<ArgumentException>(() => builder.Rename("Source", ""));
	}

	[Fact]
	public void ThrowWhenRemovePathIsNullOrEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Remove(null!));
		Should.Throw<ArgumentException>(() => builder.Remove(""));
	}

	[Fact]
	public void ThrowWhenAddDefaultPathIsNullOrEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.AddDefault(null!, "val"));
		Should.Throw<ArgumentException>(() => builder.AddDefault("", "val"));
	}

	[Fact]
	public void ThrowWhenMoveSourcePathIsNullOrEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Move(null!, "Target"));
		Should.Throw<ArgumentException>(() => builder.Move("", "Target"));
	}

	[Fact]
	public void ThrowWhenMoveTargetPathIsNullOrEmpty()
	{
		var builder = new JsonTransformRuleBuilder();
		Should.Throw<ArgumentException>(() => builder.Move("Source", null!));
		Should.Throw<ArgumentException>(() => builder.Move("Source", ""));
	}

	[Fact]
	public void AllowNullDefaultValue()
	{
		// Arrange & Act
		var rules = new JsonTransformRuleBuilder()
			.AddDefault("Nullable", null)
			.Build();

		// Assert
		rules.Count.ShouldBe(1);
		rules[0].DefaultValue.ShouldBeNull();
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
}
