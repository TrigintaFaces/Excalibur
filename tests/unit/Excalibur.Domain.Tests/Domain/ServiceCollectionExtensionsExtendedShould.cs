using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Tests.Domain;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ServiceCollectionExtensionsExtendedShould
{
	[Fact]
	[RequiresUnreferencedCode("Test uses reflection")]
	[RequiresDynamicCode("Test uses reflection")]
	public void RegisterImplementationsOfInterface()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsExtendedShould).Assembly;

		// Act
		services.AddImplementations(assembly, typeof(ITestServiceForDIScan), ServiceLifetime.Singleton);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ITestServiceForDIScan) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection")]
	[RequiresDynamicCode("Test uses reflection")]
	public void RegisterImplementationsOfInterface_WithImplementingType()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsExtendedShould).Assembly;

		// Act
		services.AddImplementations(assembly, typeof(ITestServiceForDIScan), ServiceLifetime.Transient, registerImplementingType: true);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITestServiceForDIScan));
		services.ShouldContain(sd => sd.ImplementationType == typeof(TestServiceImplForDIScan));
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection")]
	[RequiresDynamicCode("Test uses reflection")]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddImplementations(
				typeof(ServiceCollectionExtensionsExtendedShould).Assembly,
				typeof(ITestServiceForDIScan),
				ServiceLifetime.Singleton));
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection")]
	[RequiresDynamicCode("Test uses reflection")]
	public void ThrowOnNullAssembly()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(null!, typeof(ITestServiceForDIScan), ServiceLifetime.Singleton));
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection")]
	[RequiresDynamicCode("Test uses reflection")]
	public void ThrowOnNullInterfaceType()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(typeof(ServiceCollectionExtensionsExtendedShould).Assembly, null!, ServiceLifetime.Singleton));
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection")]
	[RequiresDynamicCode("Test uses reflection")]
	public void CacheAssemblyTypes()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsExtendedShould).Assembly;

		// Act — call twice to hit cache
		services.AddImplementations(assembly, typeof(ITestServiceForDIScan), ServiceLifetime.Singleton);
		services.AddImplementations(assembly, typeof(ITestServiceForDIScan), ServiceLifetime.Singleton);

		// Assert — should have double registrations (cache just speeds it up, doesn't deduplicate)
		services.Count(sd => sd.ServiceType == typeof(ITestServiceForDIScan)).ShouldBeGreaterThanOrEqualTo(2);
	}
}

// Public test types for assembly scanning (GetExportedTypes only returns public types)
#pragma warning disable CA1515 // Consider making public types internal
public interface ITestServiceForDIScan
{
	void Execute();
}

public sealed class TestServiceImplForDIScan : ITestServiceForDIScan
{
	public void Execute() { }
}
#pragma warning restore CA1515
