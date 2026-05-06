// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.DataProcessing.Processing;

/// <summary>
/// Regression test for Beads issue Excalibur.Dispatch-am5nrt:
/// DataProcessingHostedService.cs:126 and DataProcessor.cs:483 used CreateScope()
/// instead of CreateAsyncScope(), which throws InvalidOperationException when a
/// resolved service implements IAsyncDisposable (the synchronous Dispose() path
/// cannot dispose IAsyncDisposable services).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CreateAsyncScopeRegressionShould : UnitTestBase
{
	[Fact]
	public async Task NotThrow_WhenScopedServiceImplementsIAsyncDisposable()
	{
		// Arrange -- register an IAsyncDisposable orchestration manager (the bug scenario)
		var services = new ServiceCollection();
		services.AddScoped<IDataOrchestrationManager, AsyncDisposableOrchestrationManager>();
		services.EnableDataProcessingBackgroundService(opts =>
		{
			opts.PollingInterval = TimeSpan.FromMilliseconds(50);
		});
		services.AddLogging();

		var sp = services.BuildServiceProvider();
		var hostedService = sp.GetServices<IHostedService>()
			.OfType<DataProcessingHostedService>()
			.Single();

		// Act -- run for multiple cycles; each cycle creates+disposes a scope
		// Before the fix, this would throw InvalidOperationException on scope disposal
		// because the synchronous Dispose() cannot handle IAsyncDisposable services.
		await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => AsyncDisposableOrchestrationManager.DisposeAsyncCallCount >= 2,
			TimeSpan.FromSeconds(10));
		await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert -- DisposeAsync was called (not synchronous Dispose)
		AsyncDisposableOrchestrationManager.DisposeAsyncCallCount.ShouldBeGreaterThanOrEqualTo(2,
			"CreateAsyncScope should invoke DisposeAsync on IAsyncDisposable services");
		hostedService.ConsecutiveErrors.ShouldBe(0,
			"No InvalidOperationException should occur from scope disposal");
	}

	/// <summary>
	/// Fake orchestration manager implementing IAsyncDisposable to trigger the bug.
	/// Before the fix (CreateScope + using), disposing this service threw
	/// InvalidOperationException because IServiceScope.Dispose() cannot handle
	/// IAsyncDisposable. After the fix (CreateAsyncScope + await using), DisposeAsync
	/// is called correctly.
	/// </summary>
	private sealed class AsyncDisposableOrchestrationManager : IDataOrchestrationManager, IAsyncDisposable
	{
		private static int s_disposeAsyncCallCount;

		public static int DisposeAsyncCallCount => Volatile.Read(ref s_disposeAsyncCallCount);

		public Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, CancellationToken cancellationToken)
			=> Task.FromResult(Guid.NewGuid());

		public ValueTask ProcessDataTasksAsync(CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask DisposeAsync()
		{
			Interlocked.Increment(ref s_disposeAsyncCallCount);
			return ValueTask.CompletedTask;
		}
	}
}
