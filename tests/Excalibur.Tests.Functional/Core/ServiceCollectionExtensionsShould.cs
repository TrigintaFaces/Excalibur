using System.Reflection;

using Excalibur.Core;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public interface IService
{
	public string GetServiceName();
}

public class ServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterMultipleImplementationsOfSameInterface()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = services.AddImplementations<IService>(assembly, ServiceLifetime.Transient);
		var provider = services.BuildServiceProvider();

		var servicesList = provider.GetServices<IService>().ToList();

		// Assert
		servicesList.Count.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void RegisterImplementingTypeIfFlagIsTrue()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = services.AddImplementations<IService>(assembly, ServiceLifetime.Transient, true);
		var provider = services.BuildServiceProvider();

		var concreteService = provider.GetService<ConcreteServiceA>();

		// Assert
		_ = concreteService.ShouldNotBeNull();
	}

	[Fact]
	public void ResolveAllImplementationsSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = services.AddImplementations<IService>(assembly, ServiceLifetime.Transient);
		var provider = services.BuildServiceProvider();

		var servicesList = provider.GetServices<IService>().ToList();

		// Assert
		servicesList.ShouldNotBeEmpty();
		servicesList.All((IService s) => s != null).ShouldBeTrue();
	}
}

public class ConcreteServiceA : IService
{
	public string GetServiceName() => "ConcreteServiceA";
}

public class ConcreteServiceB : IService
{
	public string GetServiceName() => "ConcreteServiceB";
}
