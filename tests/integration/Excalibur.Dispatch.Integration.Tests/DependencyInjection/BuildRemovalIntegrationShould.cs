// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.DependencyInjection;

/// <summary>
/// Integration tests verifying the Build() removal from <see cref="IDispatchBuilder"/>
/// (Sprint 502, bd-7kh3o) and confirming pipeline materialization still works.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "DependencyInjection")]
public sealed class BuildRemovalIntegrationShould : IDisposable
{
	private ServiceProvider? _serviceProvider;

	public void Dispose()
	{
		_serviceProvider?.Dispose();
	}

	#region Build() Removed from Interface (AC-1)

	[Fact]
	public void NotExposeBuildMethod_OnIDispatchBuilderInterface()
	{
		// Arrange & Act
		var buildMethod = typeof(IDispatchBuilder).GetMethod(
			"Build",
			BindingFlags.Public | BindingFlags.Instance);

		// Assert — Build() should not be on the public interface
		buildMethod.ShouldBeNull(
			"IDispatchBuilder should no longer expose Build() — it was removed in Sprint 502 (bd-7kh3o)");
	}

	[Fact]
	public void NotExposeBuildMethod_InAnyInterfaceMembers()
	{
		// Verify Build is not in the interface member list at all
		var members = typeof(IDispatchBuilder).GetMembers(BindingFlags.Public | BindingFlags.Instance);
		var buildMembers = members.Where(m => m.Name == "Build").ToArray();

		buildMembers.ShouldBeEmpty(
			"IDispatchBuilder should have no members named 'Build'");
	}

	#endregion

	#region Pipeline Materialization Without Build() (AC-2, AC-3)

	[Fact]
	public void RegisterDispatcher_WhenAddDispatchCalledWithoutExplicitBuild()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — AddDispatch() should internally handle pipeline materialization
		_ = services.AddDispatch();
		_serviceProvider = services.BuildServiceProvider();

		// Assert — IDispatcher should be resolvable
		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull(
			"AddDispatch() should register IDispatcher without consumers needing to call Build()");
	}

	[Fact]
	public void RegisterDispatcher_WhenAddDispatchCalledWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — configure action variant
		_ = services.AddDispatch(builder =>
		{
			_ = builder.ConfigureOptions<Options.Configuration.DispatchOptions>(opts =>
			{
				opts.MaxConcurrency = 4;
			});
		});
		_serviceProvider = services.BuildServiceProvider();

		// Assert
		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull(
			"AddDispatch(configure) should register IDispatcher without explicit Build()");
	}

	[Fact]
	public void RegisterExpectedCoreServices_WhenAddDispatchCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch();
		_serviceProvider = services.BuildServiceProvider();

		// Assert — verify core dispatch services are registered
		_ = _serviceProvider.GetService<IDispatcher>().ShouldNotBeNull();

		// IMessageBus requires a transport binding; verify service descriptors instead
		services.ShouldContain(d => d.ServiceType == typeof(IDispatcher),
			"AddDispatch() should register IDispatcher");
	}

	#endregion

	#region Builder Extensions Still Work (AC-5 regression)

	[Fact]
	public void SupportConfigureOptions_AfterBuildRemoval()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var configureInvoked = false;

		// Act
		_ = services.AddDispatch(builder =>
		{
			_ = builder.ConfigureOptions<Options.Configuration.DispatchOptions>(opts =>
			{
				configureInvoked = true;
			});
		});

		// Assert
		configureInvoked.ShouldBeTrue("ConfigureOptions should still work after Build() removal");
	}

	[Fact]
	public void SupportConfigurePipeline_AfterBuildRemoval()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — ConfigurePipeline should accept configuration without throwing
		// Note: pipeline configure actions are stored and applied during Build(),
		// so we verify the builder accepts the call without error.
		_ = services.AddDispatch(builder =>
		{
			var result = builder.ConfigurePipeline("test-pipeline", _ => { });
			// Should return the builder for chaining
			_ = result.ShouldNotBeNull();
		});

		// Assert — should not throw; services registered
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ReturnBuilder_FromConfigurePipeline_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;
		IDispatchBuilder? returnedBuilder = null;

		// Act
		_ = services.AddDispatch(builder =>
		{
			capturedBuilder = builder;
			returnedBuilder = builder.ConfigurePipeline("test", _ => { });
		});

		// Assert — fluent chaining should still work
		_ = capturedBuilder.ShouldNotBeNull();
		_ = returnedBuilder.ShouldNotBeNull();
		returnedBuilder.ShouldBe(capturedBuilder);
	}

	#endregion

	#region IDispatchBuilder Interface Shape

	[Fact]
	public void HaveExpectedMethodCount_OnIDispatchBuilderInterface()
	{
		// The interface should have exactly these methods after Build() removal:
		// ConfigurePipeline, RegisterProfile, AddBinding, UseMiddleware, ConfigureOptions
		// Plus the Services property getter
		var methods = typeof(IDispatchBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance);

		// Filter out property accessors
		var nonPropertyMethods = methods
			.Where(m => !m.IsSpecialName)
			.ToArray();

		nonPropertyMethods.Length.ShouldBe(5,
			"IDispatchBuilder should have exactly 5 methods: ConfigurePipeline, RegisterProfile, AddBinding, UseMiddleware, ConfigureOptions");
	}

	[Fact]
	public void ExposeServicesProperty_OnIDispatchBuilderInterface()
	{
		var servicesProp = typeof(IDispatchBuilder).GetProperty("Services");
		_ = servicesProp.ShouldNotBeNull("IDispatchBuilder should still expose Services property");
		servicesProp.PropertyType.ShouldBe(typeof(IServiceCollection));
	}

	#endregion
}
