namespace Excalibur.Hosting.Tests.Jobs;

/// <summary>
/// Unit tests verifying assembly structure and exports.
/// </summary>
[Trait("Category", "Unit")]
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
	public void ExportHostingJobsServiceCollectionExtensions()
	{
		// Assert - ServiceCollectionExtensions is now in Microsoft.Extensions.DependencyInjection namespace
		_ = typeof(HostingJobsServiceCollectionExtensions).ShouldNotBeNull();
		typeof(HostingJobsServiceCollectionExtensions).IsClass.ShouldBeTrue();
		typeof(HostingJobsServiceCollectionExtensions).IsAbstract.ShouldBeTrue(); // static class
		typeof(HostingJobsServiceCollectionExtensions).IsSealed.ShouldBeTrue(); // static class
	}
}
