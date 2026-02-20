// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Unit + regression tests validating that <c>AddDispatch(Action&lt;IDispatchBuilder&gt;)</c>
/// calls <c>Build()</c> so pipeline and middleware configurations materialize into DI.
/// </summary>
/// <remarks>
/// Sprint 499 S499.5: Regression tests for the Build() bug fix (bd-ud65t).
/// The bug: <c>DispatchServiceCollectionExtensions.AddDispatch(Action&lt;IDispatchBuilder&gt;)</c>
/// created a builder with <c>using</c> but never called <c>Build()</c>, so all pipeline
/// and middleware configuration was silently lost on disposal.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
public sealed class DispatchBuilderBuildMaterializationShould : IDisposable
{
	private ServiceProvider? _serviceProvider;

	public void Dispose()
	{
		_serviceProvider?.Dispose();
	}

	#region Pipeline Materialization (AC-1)

	[Fact]
	public void MaterializePipeline_WhenConfigurePipelineCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — configure a named pipeline via the builder overload
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline("TestPipeline", _ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — the pipeline should be resolvable (Build() materialized it)
		var pipeline = _serviceProvider.GetService<IDispatchPipeline>();
		_ = pipeline.ShouldNotBeNull();
	}

	[Fact]
	public void ResolvePipelineProfileRegistry_WhenConfigureOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline("TestPipeline", _ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — IPipelineProfileRegistry must be in DI (the Build() fix
		// required adding TryAddSingleton<IPipelineProfileRegistry> to AddDispatchPipeline)
		var registry = _serviceProvider.GetService<IPipelineProfileRegistry>();
		_ = registry.ShouldNotBeNull();
	}

	[Fact]
	public void ResolveDispatcher_WhenConfigurePipelineCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline("TestPipeline", _ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — IDispatcher should resolve (Build() wires the configured dispatcher)
		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull();
	}

	[Fact]
	public void MaterializeMultiplePipelines_WhenMultipleConfigurePipelineCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — configure two named pipelines
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline("Pipeline1", _ => { });
			_ = dispatch.ConfigurePipeline("Pipeline2", _ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — pipelines should materialize (Build() processes all configurations)
		var pipeline = _serviceProvider.GetService<IDispatchPipeline>();
		_ = pipeline.ShouldNotBeNull();

		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull();
	}

	#endregion

	#region Middleware Registration (AC-2)

	[Fact]
	public void RegisterMiddleware_WhenUseMiddlewareCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — register a middleware through the builder
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.UseMiddleware<TestBuildVerificationMiddleware>();
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — middleware should be registered in DI
		using var scope = _serviceProvider.CreateScope();
		var middlewareInstances = scope.ServiceProvider.GetServices<IDispatchMiddleware>();
		middlewareInstances.ShouldContain(m => m is TestBuildVerificationMiddleware);
	}

	[Fact]
	public void RegisterMiddlewareAndPipeline_WhenBothConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — configure both pipeline and middleware
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.UseMiddleware<TestBuildVerificationMiddleware>();
			_ = dispatch.ConfigurePipeline("WithMiddleware", _ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — both should be materialized
		var pipeline = _serviceProvider.GetService<IDispatchPipeline>();
		_ = pipeline.ShouldNotBeNull();

		using var scope = _serviceProvider.CreateScope();
		var middlewareInstances = scope.ServiceProvider.GetServices<IDispatchMiddleware>();
		middlewareInstances.ShouldContain(m => m is TestBuildVerificationMiddleware);
	}

	#endregion

	#region Regression: No-Arg Overload (AC-3)

	[Fact]
	public void ContinueToWork_WhenAddDispatchCalledWithNoArguments()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — no-arg overload should still register core services
		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();

		// Assert
		_ = _serviceProvider.GetService<IDispatcher>().ShouldNotBeNull();
		_ = _serviceProvider.GetService<IDispatchPipeline>().ShouldNotBeNull();
	}

	[Fact]
	public void ContinueToWork_WhenAddDispatchCalledWithNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — null configure action should be safe (Build() still called)
		Action<IDispatchBuilder>? nullConfigure = null;
		var exception = Record.Exception(() => services.AddDispatch(nullConfigure));

		// Assert
		exception.ShouldBeNull();
		_serviceProvider = services.BuildServiceProvider();
		_ = _serviceProvider.GetService<IDispatcher>().ShouldNotBeNull();
		_ = _serviceProvider.GetService<IDispatchPipeline>().ShouldNotBeNull();
	}

	[Fact]
	public void ContinueToWork_WhenAddDispatchCalledWithEmptyConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — empty configure action (no pipeline/middleware configuration)
		_ = services.AddDispatch(_ => { });

		_serviceProvider = services.BuildServiceProvider();

		// Assert
		_ = _serviceProvider.GetService<IDispatcher>().ShouldNotBeNull();
		_ = _serviceProvider.GetService<IDispatchPipeline>().ShouldNotBeNull();
	}

	#endregion

	#region Build() Invocation Verification

	[Fact]
	public void CallBuild_AfterConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — verify Build() is called by checking that PipelineProfileSynthesizer
		// is registered (it's only registered inside Build())
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline("Verify", _ => { });
		});

		// Assert — PipelineProfileSynthesizer is registered as part of Build()
		var hasSynthesizer = services.Any(d =>
			d.ServiceType == typeof(PipelineProfileSynthesizer));
		hasSynthesizer.ShouldBeTrue("Build() should register PipelineProfileSynthesizer");
	}

	[Fact]
	public void RegisterPipelineProfileRegistry_InAddDispatchPipeline()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchPipeline();

		// Assert — IPipelineProfileRegistry must be registered by AddDispatchPipeline
		// (this was the fix that resolved 3 test failures when Build() was added)
		var hasRegistry = services.Any(d =>
			d.ServiceType == typeof(IPipelineProfileRegistry));
		hasRegistry.ShouldBeTrue(
			"AddDispatchPipeline() should register IPipelineProfileRegistry " +
			"(required by Build() → BuildRuntimeState() → UseProfile(\"Strict\"))");
	}

	#endregion

}

#region Test Fixtures (must be in separate file scope for public visibility)

/// <summary>
/// Minimal middleware for verifying DI registration via Build().
/// </summary>
public sealed class TestBuildVerificationMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken) =>
		nextDelegate(message, context, cancellationToken);
}

#endregion
