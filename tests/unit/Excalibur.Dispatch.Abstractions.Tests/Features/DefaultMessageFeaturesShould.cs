namespace Excalibur.Dispatch.Abstractions.Tests.Features;

/// <summary>
/// Unit tests for DefaultMessageFeatures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultMessageFeaturesShould : UnitTestBase
{
	[Fact]
	public void GetFeature_WhenNotSet_ReturnsNull()
	{
		// Arrange
		var features = new DefaultMessageFeatures();

		// Act
		var result = features.GetFeature<IDisposable>();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SetFeature_AndGetFeature_ReturnsStoredInstance()
	{
		// Arrange
		var features = new DefaultMessageFeatures();
		var instance = new TestFeature { Name = "test" };

		// Act
		features.SetFeature(instance);
		var result = features.GetFeature<TestFeature>();

		// Assert
		result.ShouldNotBeNull();
		result.Name.ShouldBe("test");
	}

	[Fact]
	public void SetFeature_WithNull_RemovesFeature()
	{
		// Arrange
		var features = new DefaultMessageFeatures();
		var instance = new TestFeature { Name = "test" };
		features.SetFeature(instance);

		// Act
		features.SetFeature<TestFeature>(null);
		var result = features.GetFeature<TestFeature>();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SetFeature_OverwritesPreviousValue()
	{
		// Arrange
		var features = new DefaultMessageFeatures();
		var first = new TestFeature { Name = "first" };
		var second = new TestFeature { Name = "second" };

		// Act
		features.SetFeature(first);
		features.SetFeature(second);
		var result = features.GetFeature<TestFeature>();

		// Assert
		result.ShouldNotBeNull();
		result.Name.ShouldBe("second");
	}

	[Fact]
	public void GetFeature_WrongType_ReturnsNull()
	{
		// Arrange
		var features = new DefaultMessageFeatures();
		features.SetFeature(new TestFeature { Name = "test" });

		// Act
		var result = features.GetFeature<IDisposable>();

		// Assert
		result.ShouldBeNull();
	}

	private sealed class TestFeature
	{
		public string Name { get; set; } = string.Empty;
	}
}
