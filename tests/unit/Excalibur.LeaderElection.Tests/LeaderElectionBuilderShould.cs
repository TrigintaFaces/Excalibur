// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Sprint 617 E.2: LE hardening Wave 2 tests -- ILeaderElectionBuilder interface,
/// builder implementation, Use*() extensions, With*() extensions, and DI registration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class LeaderElectionBuilderShould
{
	#region D.1: ILeaderElectionBuilder Interface Shape

	[Fact]
	public void DefineILeaderElectionBuilderWithSingleProperty()
	{
		// SA design gate: 1 property, 0 methods (matches ISagaBuilder)
		var properties = typeof(ILeaderElectionBuilder).GetProperties();
		properties.Length.ShouldBe(1, "ILeaderElectionBuilder must have exactly 1 property (Services)");

		var methods = typeof(ILeaderElectionBuilder).GetMethods()
			.Where(m => !m.IsSpecialName) // Exclude property accessors
			.ToArray();
		methods.Length.ShouldBe(0, "ILeaderElectionBuilder must have 0 methods (Use*/With* are extension methods)");
	}

	[Fact]
	public void ExposeServicesPropertyOnILeaderElectionBuilder()
	{
		var servicesProp = typeof(ILeaderElectionBuilder).GetProperty("Services");
		servicesProp.ShouldNotBeNull();
		servicesProp!.PropertyType.ShouldBe(typeof(IServiceCollection));
		servicesProp.CanRead.ShouldBeTrue();
		servicesProp.CanWrite.ShouldBeFalse("Services should be get-only");
	}

	[Fact]
	public void ImplementILeaderElectionBuilderInLeaderElectionBuilder()
	{
		typeof(ILeaderElectionBuilder).IsAssignableFrom(typeof(LeaderElectionBuilder)).ShouldBeTrue();
	}

	#endregion

	#region D.1: LeaderElectionBuilder Implementation

	[Fact]
	public void CreateBuilderWithServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var builder = new LeaderElectionBuilder(services);

		// Assert
		builder.Services.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowOnNullServiceCollection()
	{
		Should.Throw<ArgumentNullException>(() => new LeaderElectionBuilder(null!));
	}

	#endregion

	#region D.1: Builder DI Registration (AddExcaliburLeaderElection with builder)

	[Fact]
	public void RegisterViaBuilderOverload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburLeaderElection(le => le.UseInMemory());

		// Assert -- should register keyed ILeaderElectionFactory
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue(
			"UseInMemory() should register keyed ILeaderElectionFactory");
	}

	[Fact]
	public void RegisterOptionsViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburLeaderElection(le => le.UseInMemory());

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<LeaderElectionOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnServiceCollectionFromBuilderOverload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburLeaderElection(le => le.UseInMemory());

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowOnNullServicesInBuilderOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburLeaderElection(le => le.UseInMemory()));
	}

	[Fact]
	public void ThrowOnNullConfigureInBuilderOverload()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburLeaderElection((Action<ILeaderElectionBuilder>)null!));
	}

	#endregion

	#region D.1: UseInMemory() Extension

	[Fact]
	public void RegisterInMemoryFactoryViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburLeaderElection(le => le.UseInMemory());

		// Assert -- check keyed descriptor instead of resolving from provider
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();

		// Also verify InMemoryLeaderElectionFactory is registered as concrete
		services.Any(static sd =>
			sd.ServiceType == typeof(InMemoryLeaderElectionFactory) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void ReturnBuilderFromUseInMemory()
	{
		// Arrange
		var services = new ServiceCollection();
		ILeaderElectionBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburLeaderElection(le =>
		{
			var result = le.UseInMemory();
			capturedBuilder = result;
		});

		// Assert -- should return builder for fluent chaining
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilderInUseInMemory()
	{
		ILeaderElectionBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseInMemory());
	}

	#endregion

	#region D.1: WithHealthChecks() Extension

	[Fact]
	public void ReturnBuilderFromWithHealthChecks()
	{
		// Arrange
		var services = new ServiceCollection();
		ILeaderElectionBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburLeaderElection(le =>
		{
			var result = le.WithHealthChecks();
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilderInWithHealthChecks()
	{
		ILeaderElectionBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.WithHealthChecks());
	}

	#endregion

	#region D.1: WithFencingTokens() Extension

	[Fact]
	public void RegisterFencingTokenMiddlewareViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburLeaderElection(le => le
			.UseInMemory()
			.WithFencingTokens());

		// Assert -- check registration exists (don't resolve, since IFencingTokenProvider
		// must be registered separately by the consumer)
		var descriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(FencingTokenMiddleware));
		descriptor.ShouldNotBeNull("WithFencingTokens() should register FencingTokenMiddleware");
	}

	[Fact]
	public void ReturnBuilderFromWithFencingTokens()
	{
		// Arrange
		var services = new ServiceCollection();
		ILeaderElectionBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburLeaderElection(le =>
		{
			var result = le.WithFencingTokens();
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullBuilderInWithFencingTokens()
	{
		ILeaderElectionBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.WithFencingTokens());
	}

	#endregion

	#region D.1: WithOptions() Extension

	[Fact]
	public void ConfigureOptionsViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		const string expectedInstanceId = "builder-test-instance";

		// Act
		services.AddExcaliburLeaderElection(le => le
			.UseInMemory()
			.WithOptions(opts => opts.InstanceId = expectedInstanceId));

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.InstanceId.ShouldBe(expectedInstanceId);
	}

	[Fact]
	public void ThrowOnNullBuilderInWithOptions()
	{
		ILeaderElectionBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			builder.WithOptions(opts => opts.InstanceId = "test"));
	}

	[Fact]
	public void ThrowOnNullConfigureInWithOptions()
	{
		var services = new ServiceCollection();
		var builder = new LeaderElectionBuilder(services);

		Should.Throw<ArgumentNullException>(() =>
			builder.WithOptions(null!));
	}

	#endregion

	#region D.1: Fluent Chaining

	[Fact]
	public void SupportFullFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- should chain without exceptions
		services.AddExcaliburLeaderElection(le => le
			.UseInMemory()
			.WithHealthChecks()
			.WithFencingTokens()
			.WithOptions(opts => opts.InstanceId = "full-chain"));

		// Assert -- verify keyed factory is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.InstanceId.ShouldBe("full-chain");
	}

	#endregion

	#region D.1: Provider Extension Methods Exist

	[Fact]
	public void HaveUseRedisExtensionMethod()
	{
		var extensionType = typeof(RedisLeaderElectionBuilderExtensions);
		var method = extensionType.GetMethod("UseRedis",
			BindingFlags.Public | BindingFlags.Static,
			[typeof(ILeaderElectionBuilder), typeof(string)]);

		method.ShouldNotBeNull("UseRedis(builder, lockKey) must exist");
	}

	[Fact]
	public void HaveUseSqlServerExtensionMethod()
	{
		var extensionType = typeof(SqlServerLeaderElectionBuilderExtensions);
		extensionType.ShouldNotBeNull();

		var methods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "UseSqlServer")
			.ToArray();

		methods.Length.ShouldBeGreaterThan(0, "UseSqlServer must exist on SqlServerLeaderElectionBuilderExtensions");
	}

	[Fact]
	public void HaveUseConsulExtensionMethod()
	{
		var extensionType = typeof(ConsulLeaderElectionBuilderExtensions);
		extensionType.ShouldNotBeNull();

		var methods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "UseConsul")
			.ToArray();

		methods.Length.ShouldBeGreaterThan(0, "UseConsul must exist on ConsulLeaderElectionBuilderExtensions");
	}

	[Fact]
	public void HaveUseKubernetesExtensionMethod()
	{
		var extensionType = typeof(KubernetesLeaderElectionBuilderExtensions);
		extensionType.ShouldNotBeNull();

		var methods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "UseKubernetes")
			.ToArray();

		methods.Length.ShouldBeGreaterThan(0, "UseKubernetes must exist on KubernetesLeaderElectionBuilderExtensions");
	}

	#endregion
}
