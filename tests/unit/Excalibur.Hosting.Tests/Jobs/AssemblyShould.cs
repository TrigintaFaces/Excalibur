namespace Excalibur.Hosting.Tests.Jobs;

/// <summary>
/// Unit tests verifying assembly structure and exports.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class AssemblyShould : UnitTestBase
{
	[Fact]
	public void LoadSuccessfully()
	{
		// Arrange & Act
		var assembly = typeof(ExcaliburJobHostBuilderExtensions).Assembly;

		// Assert
		_ = assembly.ShouldNotBeNull();
	}

	[Fact]
	public void HaveExpectedName()
	{
		// Arrange & Act
		var assembly = typeof(ExcaliburJobHostBuilderExtensions).Assembly;

		// Assert
		assembly.GetName().Name.ShouldBe("Excalibur.Hosting.Jobs");
	}

	[Fact]
	public void ExportExcaliburJobHostBuilderExtensions()
	{
		// Assert
		_ = typeof(ExcaliburJobHostBuilderExtensions).ShouldNotBeNull();
		typeof(ExcaliburJobHostBuilderExtensions).IsClass.ShouldBeTrue();
		typeof(ExcaliburJobHostBuilderExtensions).IsAbstract.ShouldBeTrue(); // static class
		typeof(ExcaliburJobHostBuilderExtensions).IsSealed.ShouldBeTrue(); // static class
	}

	[Fact]
	public void ExportJobsExcaliburBuilderExtensions()
	{
		// Assert - canonical public path is the IExcaliburBuilder bridge (S804 FORGE-C / bd-sdhocq)
		_ = typeof(JobsExcaliburBuilderExtensions).ShouldNotBeNull();
		typeof(JobsExcaliburBuilderExtensions).IsClass.ShouldBeTrue();
		typeof(JobsExcaliburBuilderExtensions).IsAbstract.ShouldBeTrue(); // static class
		typeof(JobsExcaliburBuilderExtensions).IsSealed.ShouldBeTrue(); // static class
	}
}
