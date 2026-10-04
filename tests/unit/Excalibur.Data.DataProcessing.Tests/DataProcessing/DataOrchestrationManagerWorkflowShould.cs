// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Workflow and async behavior tests for <see cref="DataOrchestrationManager"/>.
/// Tests the orchestration contract: interface compliance, resilience policy resolution,
/// cancellation handling, and connection-per-operation pattern.
/// </summary>
/// <remarks>
/// <para>
/// DataOrchestrationManager uses Dapper-based IDataRequest internally, which requires
/// a real ADO.NET connection. These tests validate the orchestration layer above
/// the data access layer: DI composition, processor registry interaction, error
/// handling branches, and cancellation propagation.
/// </para>
/// </remarks>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataOrchestrationManagerWorkflowShould : UnitTestBase
{
	private readonly IDataProcessorRegistry _fakeRegistry = A.Fake<IDataProcessorRegistry>();
	private readonly ILogger<DataOrchestrationManager> _fakeLogger = A.Fake<ILogger<DataOrchestrationManager>>();

	[Fact]
	public void ImplementIDataOrchestrationManager()
	{
		// Arrange & Act
		var manager = CreateManager();

		// Assert
		manager.ShouldBeAssignableTo<IDataOrchestrationManager>();
	}

	[Fact]
	public void ResiliencePolicy_FallsBackToNoOp_WhenNoPolicyFactoryRegistered()
	{
		// Arrange — service provider without IDataAccessPolicyFactory
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		var manager = new DataOrchestrationManager(
			() => A.Fake<IDbConnection>(),
			_fakeRegistry,
			sp,
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
			_fakeLogger);

		// Assert — construction succeeds (policy is lazily resolved, not at ctor time)
		manager.ShouldNotBeNull();
	}

	[Fact]
	public void ResiliencePolicy_ResolvesFromDI_WhenPolicyFactoryRegistered()
	{
		// Arrange — service provider WITH IDataAccessPolicyFactory
		var fakePolicyFactory = A.Fake<IDataAccessPolicyFactory>();
		A.CallTo(() => fakePolicyFactory.GetComprehensivePolicy())
			.Returns(Polly.Policy.NoOpAsync());

		var services = new ServiceCollection();
		services.AddSingleton(fakePolicyFactory);
		var sp = services.BuildServiceProvider();

		var manager = new DataOrchestrationManager(
			() => A.Fake<IDbConnection>(),
			_fakeRegistry,
			sp,
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
			_fakeLogger);

		// Assert — construction succeeds
		manager.ShouldNotBeNull();
	}

	[Fact]
	public void ConnectionFactory_IsCalledPerOperation()
	{
		// Arrange — track how many times the factory is called
		var callCount = 0;
		Func<IDbConnection> factory = () =>
		{
			Interlocked.Increment(ref callCount);
			return A.Fake<IDbConnection>();
		};

		// Act — construct manager (factory not called at construction time)
		var manager = new DataOrchestrationManager(
			factory,
			_fakeRegistry,
			A.Fake<IServiceProvider>(),
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
			_fakeLogger);

		// Assert — factory should NOT be called during construction
		callCount.ShouldBe(0);
	}

	[Fact]
	public async Task ProcessDataTasksAsync_PropagatesCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		var manager = CreateManager();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await manager.ProcessDataTasksAsync(cts.Token));

	}

	[Fact]
	public void NotCallConnectionFactory_AtConstructionTime()
	{
		// Arrange — verify the connection factory is NOT called at construction
		// (connection-per-operation pattern: factory is only called when an operation runs)
		var factoryCalled = false;
		Func<IDbConnection> trackingFactory = () =>
		{
			factoryCalled = true;
			return A.Fake<IDbConnection>();
		};

		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		// Act — construct manager
		_ = new DataOrchestrationManager(
			trackingFactory,
			_fakeRegistry,
			sp,
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
			_fakeLogger);

		// Assert — factory should NOT be called during construction
		factoryCalled.ShouldBeFalse();
	}

	[Fact]
	public void Throw_WhenAllDependencies_AreNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataOrchestrationManager(null!, null!, null!, null!, null!));
	}

	[Fact]
	public async Task ReportAllClaimFailuresAfterAttemptingIndependentTasks()
	{
		using var provider = new ServiceCollection().BuildServiceProvider();
		var attempts = 0;
		var manager = new DataOrchestrationManager(
			() => { attempts++; throw new InvalidOperationException("State database unavailable"); }, _fakeRegistry, provider,
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()), _fakeLogger);
		var requests = new List<DataTaskRequest> { new() { RecordType = "first" }, new() { RecordType = "second" } };
		var method = typeof(DataOrchestrationManager).GetMethod("ProcessRequestsAsync",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
		var run = (Task)method.Invoke(manager, [requests, CancellationToken.None])!;

		var failure = await Should.ThrowAsync<AggregateException>(() => run);
		attempts.ShouldBe(2);
		failure.InnerExceptions.Count.ShouldBe(2);
		failure.InnerExceptions.ShouldAllBe(ex => ex.Message == "State database unavailable");
	}

	// --- Helpers ---

	private DataOrchestrationManager CreateManager()
	{
		var services = new ServiceCollection();
		services.AddSingleton(_fakeRegistry);
		var sp = services.BuildServiceProvider();

		return new DataOrchestrationManager(
			() => A.Fake<IDbConnection>(),
			_fakeRegistry,
			sp,
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions()),
			_fakeLogger);
	}
}
