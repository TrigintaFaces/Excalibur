using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.A3.Authorization;
using Excalibur.Core;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Unit.A3;

public sealed class ServiceCollectionExtensionsShould : IDisposable
{
	private readonly Dictionary<string, string?> _originalContext;

	public ServiceCollectionExtensionsShould()
	{
		// Save original values from the context
		_originalContext = new Dictionary<string, string?>();
		_originalContext["AuthenticationServiceEndpoint"] = ApplicationContext.Get("AuthenticationServiceEndpoint", string.Empty);
		_originalContext["AuthorizationServiceEndpoint"] = ApplicationContext.Get("AuthorizationServiceEndpoint", string.Empty);

		// Initialize the context with test values
		var testContext = new Dictionary<string, string?>
		{
			["AuthenticationServiceEndpoint"] = "https://auth-test.example.com",
			["AuthorizationServiceEndpoint"] = "https://authz-test.example.com"
		};

		ApplicationContext.Init(testContext);
	}

	public void Dispose()
	{
		// Restore original context
		ApplicationContext.Init(_originalContext);
	}

	[Fact]
	public void AddA3MediatRServicesCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddA3MediatRServices();

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeSameAs(services);

		// Verify MediatR and Pipeline Behaviors
		ContainsTransient(services, typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>)).ShouldBeTrue();
		ContainsTransient(services, typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>)).ShouldBeTrue();
	}

	private static bool ContainsTransient(IServiceCollection services, Type serviceType, Type implementationType) => services.Any(
		descriptor =>
			descriptor.ServiceType == serviceType &&
			descriptor.ImplementationType == implementationType &&
			descriptor.Lifetime == ServiceLifetime.Transient);

	private static bool ContainsScoped(IServiceCollection services, Type serviceType, Type implementationType) => services.Any(
		descriptor =>
			descriptor.ServiceType == serviceType &&
			descriptor.ImplementationType == implementationType &&
			descriptor.Lifetime == ServiceLifetime.Scoped);

	private static bool ContainsSingleton(IServiceCollection services, Type serviceType) => services.Any(
		descriptor =>
			descriptor.ServiceType == serviceType &&
			descriptor.Lifetime == ServiceLifetime.Singleton);
}
