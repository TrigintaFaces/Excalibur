using System.Reflection;

using Excalibur.Core;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public interface IService
{
	public string GetServiceName();
}

public interface INonExistentService
{
}

public class ServiceCollectionExtensionsShould
{
	[Fact]
	public void ShouldRegisterAndResolveDependenciesCorrectly()
	{
		var services = new ServiceCollection();
		_ = services.AddImplementations<IClientAddress>(typeof(ClientAddress).Assembly, ServiceLifetime.Scoped, registerImplementingType: true);
		var serviceProvider = services.BuildServiceProvider();

		using var scope = serviceProvider.CreateScope();
		var clientAddress = scope.ServiceProvider.GetRequiredService<IClientAddress>();

		_ = clientAddress.ShouldNotBeNull();
		_ = clientAddress.ShouldBeOfType<ClientAddress>();
	}

	[Fact]
	public void RegisterServicesFromCurrentAssembly()
	{
		// Arrange
		var services = new ServiceCollection();
		var currentAssembly = Assembly.GetExecutingAssembly();

		// Act
		_ = services.AddImplementations<IService>(currentAssembly, ServiceLifetime.Transient);
		var provider = services.BuildServiceProvider();

		var service = provider.GetService<IService>();

		// Assert
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public void HandleMissingImplementationsGracefully()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = Assembly.GetExecutingAssembly();

		// Act
		_ = services.AddImplementations<INonExistentService>(assembly, ServiceLifetime.Transient);
		var provider = services.BuildServiceProvider();

		var service = provider.GetService<INonExistentService>();

		// Assert
		service.ShouldBeNull();
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
