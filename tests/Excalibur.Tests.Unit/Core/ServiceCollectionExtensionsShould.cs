using System.Reflection;

using Excalibur.Core;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public interface IService
{
	public string GetServiceName();
}

public class ServiceCollectionExtensionsShould
{
	private readonly IServiceCollection _services = new ServiceCollection();

	[Fact]
	public void ThrowArgumentNullExceptionIfAssemblyIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _services.AddImplementations<IServiceCollection>(null!, ServiceLifetime.Transient));
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfInterfaceTypeIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => _services.AddImplementations(Assembly.GetExecutingAssembly(), null!, ServiceLifetime.Transient));
	}

	[Fact]
	public void RegisterTransientServicesCorrectly()
	{
		// Arrange
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = _services.AddImplementations<IService>(assembly, ServiceLifetime.Transient);

		// Assert
		var provider = _services.BuildServiceProvider();
		var service1 = provider.GetService<IService>();
		var service2 = provider.GetService<IService>();

		_ = service1.ShouldNotBeNull();
		_ = service2.ShouldNotBeNull();
		service1.ShouldNotBe(service2); // Transient: Different instances
	}

	[Fact]
	public void RegisterSingletonServicesCorrectly()
	{
		// Arrange
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = _services.AddImplementations<IService>(assembly, ServiceLifetime.Singleton);

		// Assert
		var provider = _services.BuildServiceProvider();
		var service1 = provider.GetService<IService>();
		var service2 = provider.GetService<IService>();

		_ = service1.ShouldNotBeNull();
		_ = service2.ShouldNotBeNull();
		service1.ShouldBe(service2); // Singleton: Same instance
	}

	[Fact]
	public void RegisterScopedServicesCorrectly()
	{
		// Arrange
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = _services.AddImplementations<IService>(assembly, ServiceLifetime.Scoped);

		// Assert
		var provider = _services.BuildServiceProvider();
		using var scope1 = provider.CreateScope();
		using var scope2 = provider.CreateScope();

		var service1 = scope1.ServiceProvider.GetService<IService>();
		var service2 = scope1.ServiceProvider.GetService<IService>();
		var service3 = scope2.ServiceProvider.GetService<IService>();

		service1.ShouldBe(service2);
		service1.ShouldNotBe(service3);
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
