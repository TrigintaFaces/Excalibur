using Excalibur.EventSourcing.Upcasting;

namespace Excalibur.EventSourcing.Tests.Upcasting;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonTransformRuleShould
{
	[Fact]
	public void StoreOperationAndPath()
	{
		// Arrange & Act
		var rule = new JsonTransformRule(JsonTransformOperation.Remove, "FieldName");

		// Assert
		rule.Operation.ShouldBe(JsonTransformOperation.Remove);
		rule.Path.ShouldBe("FieldName");
		rule.TargetPath.ShouldBeNull();
		rule.DefaultValue.ShouldBeNull();
	}

	[Fact]
	public void StoreTargetPath()
	{
		// Arrange & Act
		var rule = new JsonTransformRule(JsonTransformOperation.Rename, "Source", "Target");

		// Assert
		rule.TargetPath.ShouldBe("Target");
	}

	[Fact]
	public void StoreDefaultValue()
	{
		// Arrange & Act
		var rule = new JsonTransformRule(JsonTransformOperation.AddDefault, "NewProp", DefaultValue: 42);

		// Assert
		rule.DefaultValue.ShouldBe(42);
	}

	[Fact]
	public void SupportEqualityComparison()
	{
		// Arrange
		var rule1 = new JsonTransformRule(JsonTransformOperation.Remove, "Field");
		var rule2 = new JsonTransformRule(JsonTransformOperation.Remove, "Field");

		// Assert
		rule1.ShouldBe(rule2);
	}

	[Fact]
	public void DetectInequalityByOperation()
	{
		// Arrange
		var rule1 = new JsonTransformRule(JsonTransformOperation.Remove, "Field");
		var rule2 = new JsonTransformRule(JsonTransformOperation.AddDefault, "Field");

		// Assert
		rule1.ShouldNotBe(rule2);
	}

	[Fact]
	public void DetectInequalityByPath()
	{
		// Arrange
		var rule1 = new JsonTransformRule(JsonTransformOperation.Remove, "FieldA");
		var rule2 = new JsonTransformRule(JsonTransformOperation.Remove, "FieldB");

		// Assert
		rule1.ShouldNotBe(rule2);
	}

	[Theory]
	[InlineData(JsonTransformOperation.Rename)]
	[InlineData(JsonTransformOperation.Remove)]
	[InlineData(JsonTransformOperation.AddDefault)]
	[InlineData(JsonTransformOperation.Move)]
	public void SupportAllOperationTypes(JsonTransformOperation operation)
	{
		// Arrange & Act
		var rule = new JsonTransformRule(operation, "Path");

		// Assert
		rule.Operation.ShouldBe(operation);
	}

	[Fact]
	public void ImplementToStringFromRecord()
	{
		// Arrange
		var rule = new JsonTransformRule(JsonTransformOperation.Rename, "Source", "Target");

		// Act
		var str = rule.ToString();

		// Assert
		str.ShouldNotBeNullOrWhiteSpace();
		str.ShouldContain("Rename");
		str.ShouldContain("Source");
	}
}
