// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

using Xunit.Abstractions;

namespace Excalibur.Tests.DependencyInjection;

/// <summary>
/// Wrapper DI compliance tests per ADR-078 Dispatch-Excalibur Boundary Contract.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that all <c>AddExcalibur*</c> methods properly delegate to
/// <c>AddDispatch()</c> to ensure Dispatch primitives are always available when
/// using Excalibur.
/// </para>
/// <para>
/// ADR-078 establishes that:
/// <list type="bullet">
/// <item>Dispatch owns messaging primitives (IDispatcher, IMessageBus, etc.)</item>
/// <item>Excalibur owns application framework (aggregates, event stores, etc.)</item>
/// <item>Dependency direction: Excalibur -> Dispatch (one-way)</item>
/// <item>All AddExcalibur* must call AddDispatch* internally</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class WrapperDIComplianceShould
{
	private readonly ITestOutputHelper _output;

	public WrapperDIComplianceShould(ITestOutputHelper output)
	{
		_output = output;
	}

	#region AddExcaliburEventSourcing Compliance

	/// <summary>
	/// Verifies that AddExcaliburEventSourcing registers IDispatcher.
	/// </summary>
	/// <remarks>
	/// Per ADR-078, AddExcaliburEventSourcing must call AddDispatch() internally,
	/// which registers IDispatcher as a core messaging primitive.
	/// </remarks>
	[Fact]
	public void AddExcaliburEventSourcing_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburEventSourcing();
		var provider = services.BuildServiceProvider();

		// Assert - IDispatcher should be registered by AddDispatch()
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburEventSourcing should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburEventSourcing correctly registered IDispatcher");
	}

	/// <summary>
	/// Verifies that AddExcaliburEventSourcing registers IMessageBus.
	/// </summary>
	[Fact]
	public void AddExcaliburEventSourcing_ShouldRegisterMessageBus()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburEventSourcing();
		var provider = services.BuildServiceProvider();

		// Assert - IMessageBus should be registered by AddDispatch()
		var messageBus = provider.GetKeyedService<IMessageBus>("Local");
		_ = messageBus.ShouldNotBeNull("AddExcaliburEventSourcing should register the Local IMessageBus via AddDispatch()");

		_output.WriteLine("AddExcaliburEventSourcing correctly registered IMessageBus");
	}

	/// <summary>
	/// Verifies that AddExcaliburEventSourcing with builder also registers Dispatch primitives.
	/// </summary>
	[Fact]
	public void AddExcaliburEventSourcing_WithBuilder_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburEventSourcing(builder =>
		{
			// Configure with any builder options
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburEventSourcing(builder) should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburEventSourcing(builder) correctly registered IDispatcher");
	}

	#endregion AddExcaliburEventSourcing Compliance

	#region AddExcaliburSaga Compliance

	/// <summary>
	/// Verifies that AddExcaliburSaga registers IDispatcher.
	/// </summary>
	/// <remarks>
	/// Per ADR-078, AddExcaliburSaga must call AddDispatch() internally.
	/// </remarks>
	[Fact]
	public void AddExcaliburSaga_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburSaga();
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburSaga should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburSaga correctly registered IDispatcher");
	}

	/// <summary>
	/// Verifies that AddExcaliburSaga registers IMessageBus.
	/// </summary>
	[Fact]
	public void AddExcaliburSaga_ShouldRegisterMessageBus()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburSaga();
		var provider = services.BuildServiceProvider();

		// Assert
		var messageBus = provider.GetKeyedService<IMessageBus>("Local");
		_ = messageBus.ShouldNotBeNull("AddExcaliburSaga should register the Local IMessageBus via AddDispatch()");

		_output.WriteLine("AddExcaliburSaga correctly registered IMessageBus");
	}

	/// <summary>
	/// Verifies that AddExcaliburSaga with options also registers Dispatch primitives.
	/// </summary>
	[Fact]
	public void AddExcaliburSaga_WithOptions_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburSaga(options =>
		{
			options.MaxConcurrency = 5;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburSaga(options) should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburSaga(options) correctly registered IDispatcher");
	}

	#endregion AddExcaliburSaga Compliance

	#region AddExcaliburOutbox Compliance

	/// <summary>
	/// Verifies that AddExcaliburOutbox registers IDispatcher.
	/// </summary>
	/// <remarks>
	/// Per ADR-078, AddExcaliburOutbox must call AddDispatch() internally.
	/// </remarks>
	[Fact]
	public void AddExcaliburOutbox_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburOutbox();
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburOutbox should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburOutbox correctly registered IDispatcher");
	}

	/// <summary>
	/// Verifies that AddExcaliburOutbox registers IMessageBus.
	/// </summary>
	[Fact]
	public void AddExcaliburOutbox_ShouldRegisterMessageBus()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburOutbox();
		var provider = services.BuildServiceProvider();

		// Assert
		var messageBus = provider.GetKeyedService<IMessageBus>("Local");
		_ = messageBus.ShouldNotBeNull("AddExcaliburOutbox should register the Local IMessageBus via AddDispatch()");

		_output.WriteLine("AddExcaliburOutbox correctly registered IMessageBus");
	}

	/// <summary>
	/// Verifies that AddExcaliburOutbox with options also registers Dispatch primitives.
	/// </summary>
	[Fact]
	public void AddExcaliburOutbox_WithOptions_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - Use the preset-based fluent API (ADR-098)
		_ = services.AddExcaliburOutbox(
			Excalibur.Outbox.OutboxOptions.Balanced()
				.WithBatchSize(50)
				.Build());
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburOutbox(options) should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburOutbox(options) correctly registered IDispatcher");
	}

	#endregion AddExcaliburOutbox Compliance

	#region Multiple Wrapper Calls - Idempotency

	/// <summary>
	/// Verifies that calling multiple AddExcalibur* methods doesn't duplicate Dispatch registrations.
	/// </summary>
	[Fact]
	public void MultipleAddExcaliburCalls_ShouldNotDuplicateDispatchRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - Call all three wrappers
		_ = services.AddExcaliburEventSourcing();
		_ = services.AddExcaliburSaga();
		_ = services.AddExcaliburOutbox();

		// Assert - Should only have one IDispatcher registration
		var dispatcherRegistrations = services.Count(s => s.ServiceType == typeof(IDispatcher));
		dispatcherRegistrations.ShouldBe(1, "Multiple AddExcalibur* calls should not duplicate IDispatcher registration");

		var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull();

		_output.WriteLine($"IDispatcher registration count: {dispatcherRegistrations} (should be 1)");
	}

	#endregion Multiple Wrapper Calls - Idempotency

	#region AddExcaliburBaseServices (Hosting) Compliance

	/// <summary>
	/// Verifies that AddExcaliburBaseServices registers IDispatcher.
	/// </summary>
	/// <remarks>
	/// Per ADR-078, AddExcaliburBaseServices must call AddDispatch() internally.
	/// Sprint 126 task xxula implemented this fix in Excalibur.Hosting.
	/// </remarks>
	[Fact]
	public void AddExcaliburBaseServices_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburBaseServices(assemblies: [GetType().Assembly]);
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburBaseServices should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburBaseServices correctly registered IDispatcher");
	}

	/// <summary>
	/// Verifies that AddExcaliburBaseServices registers IMessageBus.
	/// </summary>
	[Fact]
	public void AddExcaliburBaseServices_ShouldRegisterMessageBus()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburBaseServices(assemblies: [GetType().Assembly]);
		var provider = services.BuildServiceProvider();

		// Assert
		var messageBus = provider.GetKeyedService<IMessageBus>("Local");
		_ = messageBus.ShouldNotBeNull("AddExcaliburBaseServices should register the Local IMessageBus via AddDispatch()");

		_output.WriteLine("AddExcaliburBaseServices correctly registered IMessageBus");
	}

	/// <summary>
	/// Verifies that AddExcaliburBaseServices with tenant options also registers Dispatch primitives.
	/// </summary>
	[Fact]
	public void AddExcaliburBaseServices_WithTenant_ShouldRegisterDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburBaseServices(
			assemblies: [GetType().Assembly],
			useLocalClientAddress: true,
			tenantId: "test-tenant");
		var provider = services.BuildServiceProvider();

		// Assert
		var dispatcher = provider.GetService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("AddExcaliburBaseServices with tenant should register IDispatcher via AddDispatch()");

		_output.WriteLine("AddExcaliburBaseServices with tenant correctly registered IDispatcher");
	}

	#endregion AddExcaliburBaseServices (Hosting) Compliance

	#region Extended Integration - Handler Resolution

	/// <summary>
	/// Verifies that configuring via AddExcaliburEventSourcing enables functional Dispatch handler resolution.
	/// </summary>
	/// <remarks>
	/// Per SoftwareArchitect guidance in Sprint 126, this validates that the Dispatch pipeline
	/// is fully functional when only configuring via Excalibur wrapper methods.
	/// </remarks>
	[Fact]
	public void AddExcaliburEventSourcing_ShouldEnableDispatchPipeline()
	{
		// Arrange - Configure only via Excalibur
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburEventSourcing();
		var provider = services.BuildServiceProvider();

		// Assert - Verify Dispatch pipeline is functional
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("Dispatch pipeline should be functional via AddExcaliburEventSourcing");

		_output.WriteLine("AddExcaliburEventSourcing enables functional Dispatch pipeline");
	}

	/// <summary>
	/// Verifies that configuring via AddExcaliburSaga enables messaging capabilities.
	/// </summary>
	[Fact]
	public void AddExcaliburSaga_ShouldEnableMessaging()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburSaga();
		var provider = services.BuildServiceProvider();

		// Assert
		var messageBus = provider.GetRequiredKeyedService<IMessageBus>("Local");
		_ = messageBus.ShouldNotBeNull("Messaging should be enabled via AddExcaliburSaga");

		_output.WriteLine("AddExcaliburSaga enables messaging capabilities");
	}

	/// <summary>
	/// Verifies that configuring via AddExcaliburOutbox enables transport capabilities.
	/// </summary>
	[Fact]
	public void AddExcaliburOutbox_ShouldEnableTransport()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburOutbox();
		var provider = services.BuildServiceProvider();

		// Assert
		var messageBus = provider.GetRequiredKeyedService<IMessageBus>("Local");
		_ = messageBus.ShouldNotBeNull("Transport should be enabled via AddExcaliburOutbox");

		_output.WriteLine("AddExcaliburOutbox enables transport capabilities");
	}

	#endregion Extended Integration - Handler Resolution

	#region Complete Wrapper Stack

	/// <summary>
	/// Verifies that a complete Excalibur stack correctly initializes all Dispatch primitives.
	/// </summary>
	/// <remarks>
	/// This test validates the full integration scenario where consumers configure
	/// EventSourcing + Saga + Outbox + BaseServices together.
	/// </remarks>
	[Fact]
	public void CompleteExcaliburStack_ShouldRegisterAllDispatchPrimitives()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - Full Excalibur stack
		_ = services.AddExcaliburBaseServices(assemblies: [GetType().Assembly]);
		_ = services.AddExcaliburEventSourcing();
		_ = services.AddExcaliburSaga();
		_ = services.AddExcaliburOutbox();
		var provider = services.BuildServiceProvider();

		// Assert - All primitives available
		var dispatcher = provider.GetService<IDispatcher>();
		var messageBus = provider.GetKeyedService<IMessageBus>("Local");

		_ = dispatcher.ShouldNotBeNull("Full Excalibur stack should register IDispatcher");
		_ = messageBus.ShouldNotBeNull("Full Excalibur stack should register the Local IMessageBus");

		// Verify no duplicates
		var dispatcherCount = services.Count(s => s.ServiceType == typeof(IDispatcher));
		var messageBusCount = services.Count(s => s.ServiceType == typeof(IMessageBus));

		dispatcherCount.ShouldBe(1, "Full stack should have exactly one IDispatcher registration");
		messageBusCount.ShouldBe(1, "Full stack should have exactly one IMessageBus registration");

		_output.WriteLine($"Complete Excalibur stack: IDispatcher={dispatcherCount}, IMessageBus={messageBusCount}");
	}

	#endregion Complete Wrapper Stack
}
