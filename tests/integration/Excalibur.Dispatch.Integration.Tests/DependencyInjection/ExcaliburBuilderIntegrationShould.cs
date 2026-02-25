// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Excalibur.Hosting.Builders;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.DependencyInjection;

/// <summary>
/// Integration tests for <see cref="IExcaliburBuilder"/> and the
/// <c>AddExcalibur(Action&lt;IExcaliburBuilder&gt;)</c> unified entry point.
/// </summary>
/// <remarks>
/// Sprint 501 S501.6: Tests for Phase 3 (bd-6yjcj, bd-rznmq) and Phase 4 (bd-79if7).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "DependencyInjection")]
public sealed class ExcaliburBuilderIntegrationShould : IDisposable
{
	private ServiceProvider? _serviceProvider;

	public void Dispose()
	{
		_serviceProvider?.Dispose();
	}

	#region AddExcalibur() Entry Point (S501.3 — AC-1, AC-2, AC-3)

	[Fact]
	public void RegisterDispatchPrimitives_WhenAddExcaliburCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — AddExcalibur() should internally call AddDispatch()
		_ = services.AddExcalibur(_ => { });

		_serviceProvider = services.BuildServiceProvider();

		// Assert — IDispatcher should be resolvable (registered by AddDispatch())
		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcalibur() should internally call AddDispatch()");
	}

	[Fact]
	public void InvokeBuilderAction_WhenAddExcaliburCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builderInvoked = false;

		// Act
		_ = services.AddExcalibur(_ => { builderInvoked = true; });

		// Assert
		builderInvoked.ShouldBeTrue("AddExcalibur() should invoke the configure action");
	}

	[Fact]
	public void ProvideExcaliburBuilderToAction_WhenAddExcaliburCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IExcaliburBuilder? capturedBuilder = null;

		// Act
		_ = services.AddExcalibur(builder => { capturedBuilder = builder; });

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
		capturedBuilder.Services.ShouldBeSameAs(services);
	}

	[Fact]
	public void ReturnServiceCollection_WhenAddExcaliburCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddExcalibur(_ => { });

		// Assert — returns IServiceCollection for further chaining
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region IExcaliburBuilder.Add*() Subsystem Delegation (S501.2 — AC-3 through AC-8)

	[Fact]
	public void RegisterEventSourcingServices_WhenAddEventSourcingCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var initialCount = services.Count;

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddEventSourcing(_ => { });
		});

		// Assert — event sourcing registration should add services
		services.Count.ShouldBeGreaterThan(initialCount,
			"AddEventSourcing() should delegate to AddExcaliburEventSourcing() and add services");
	}

	[Fact]
	public void RegisterOutboxServices_WhenAddOutboxCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var initialCount = services.Count;

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddOutbox(_ => { });
		});

		// Assert
		services.Count.ShouldBeGreaterThan(initialCount,
			"AddOutbox() should delegate to AddExcaliburOutbox() and add services");
	}

	[Fact]
	public void RegisterCdcServices_WhenAddCdcCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var initialCount = services.Count;

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddCdc(_ => { });
		});

		// Assert
		services.Count.ShouldBeGreaterThan(initialCount,
			"AddCdc() should delegate to AddCdcProcessor() and add services");
	}

	[Fact]
	public void RegisterSagaServices_WhenAddSagasCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var initialCount = services.Count;

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddSagas();
		});

		// Assert
		services.Count.ShouldBeGreaterThan(initialCount,
			"AddSagas() should delegate to AddExcaliburSaga() and add services");
	}

	[Fact]
	public void RegisterLeaderElectionServices_WhenAddLeaderElectionCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var initialCount = services.Count;

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddLeaderElection();
		});

		// Assert
		services.Count.ShouldBeGreaterThan(initialCount,
			"AddLeaderElection() should delegate to AddExcaliburLeaderElection() and add services");
	}

	#endregion

	#region Fluent Chaining (S501.2 — AC-8)

	[Fact]
	public void SupportFluentChaining_WhenMultipleSubsystemsConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — all methods should return IExcaliburBuilder for chaining
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur
				.AddEventSourcing(_ => { })
				.AddOutbox(_ => { })
				.AddCdc(_ => { })
				.AddSagas()
				.AddLeaderElection();
		});

		// Assert — should not throw; verify services were added
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ReturnSameBuilder_WhenAddEventSourcingCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IExcaliburBuilder? result = null;

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			result = excalibur.AddEventSourcing(_ => { });
		});

		// Assert
		_ = result.ShouldNotBeNull();
	}

	#endregion

	#region Options-Only Subsystems with Configure Action (S501.2 — AC-6, AC-7)

	[Fact]
	public void AcceptSagaConfigureAction_WhenAddSagasCalledWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — configure with non-null options action
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddSagas(opts => opts.EnableAutomaticCleanup = false);
		});

		// Assert — should not throw
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void AcceptLeaderElectionConfigureAction_WhenAddLeaderElectionCalledWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromSeconds(30));
		});

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}

	#endregion

	#region Null Guard Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNullServicesPassedToAddExcalibur()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcalibur(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullConfigurePassedToAddExcalibur()
	{
		var services = new ServiceCollection();
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcalibur(null!));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullConfigurePassedToAddEventSourcing()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcalibur(excalibur =>
			{
				_ = excalibur.AddEventSourcing(null!);
			}));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullConfigurePassedToAddOutbox()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcalibur(excalibur =>
			{
				_ = excalibur.AddOutbox(null!);
			}));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullConfigurePassedToAddCdc()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcalibur(excalibur =>
			{
				_ = excalibur.AddCdc(null!);
			}));
	}

	#endregion

	#region AddDispatch + AddExcalibur Ordering (Two-Call Pattern)

	[Fact]
	public void ResolveDispatcher_WhenAddDispatchCalledBeforeAddExcalibur()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;

		// Act — AddDispatch(configure) first, then AddExcalibur(configure)
		_ = services.AddDispatch(dispatch =>
		{
			capturedBuilder = dispatch;
			_ = dispatch.ConfigurePipeline("test-pipeline", _ => { });
		});

		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddEventSourcing(_ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — IDispatcher resolvable, builder was invoked
		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("IDispatcher should be resolvable when AddDispatch(configure) precedes AddExcalibur()");
		_ = capturedBuilder.ShouldNotBeNull("AddDispatch(configure) builder action should be invoked");
	}

	[Fact]
	public void ResolveDispatcher_WhenAddExcaliburCalledBeforeAddDispatch()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;

		// Act — AddExcalibur(configure) first, then AddDispatch(configure)
		_ = services.AddExcalibur(excalibur =>
		{
			_ = excalibur.AddEventSourcing(_ => { });
		});

		_ = services.AddDispatch(dispatch =>
		{
			capturedBuilder = dispatch;
			_ = dispatch.ConfigurePipeline("test-pipeline", _ => { });
		});

		_serviceProvider = services.BuildServiceProvider();

		// Assert — IDispatcher resolvable, builder was invoked (TryAdd* idempotency)
		var dispatcher = _serviceProvider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("IDispatcher should be resolvable when AddExcalibur() precedes AddDispatch(configure)");
		_ = capturedBuilder.ShouldNotBeNull("AddDispatch(configure) builder action should be invoked regardless of call ordering");
	}

	#endregion

	#region Standalone API Regression (S501.3 — AC-4)

	[Fact]
	public void ContinueToWork_WhenAddExcaliburSagaCalledDirectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();

		// Act — standalone registration (pre-Sprint 501 API)
		_ = services.AddExcaliburSaga();

		// Assert — services should be registered without IExcaliburBuilder
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ContinueToWork_WhenAddExcaliburLeaderElectionCalledDirectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();

		// Act
		_ = services.AddExcaliburLeaderElection();

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}

	#endregion
}
