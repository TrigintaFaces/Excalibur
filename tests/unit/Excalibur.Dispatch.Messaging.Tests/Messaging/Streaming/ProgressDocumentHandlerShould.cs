// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Tests.Messaging.Streaming.TestTypes;

namespace Excalibur.Dispatch.Tests.Messaging.Streaming;

/// <summary>
/// Tests for <see cref="IProgressDocumentHandler{TDocument}"/> interface and dispatcher integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProgressDocumentHandlerShould
{
	[Fact]
	public async Task ResolveAndInvokeProgressHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(10);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();

		var progress = new Progress<DocumentProgress>(p => progressReports.Add(p));

		// Act
		await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);

		// Small delay to ensure all progress reports are collected
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Assert
		progressReports.Count.ShouldBeGreaterThan(0);
		progressReports.Last().PercentComplete.ShouldBe(100.0);
		progressReports.Last().CurrentPhase.ShouldBe("Processing complete");
	}

	[Fact]
	public async Task ReportProgressIncrementally()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(5);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();

		var progress = new Progress<DocumentProgress>(p => progressReports.Add(p));

		// Act
		await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Assert - progress should be monotonically increasing
		var percentages = progressReports.Select(p => p.PercentComplete).Where(p => p >= 0).ToList();
		for (var i = 1; i < percentages.Count; i++)
		{
			percentages[i].ShouldBeGreaterThanOrEqualTo(percentages[i - 1]);
		}
	}

	[Fact]
	public async Task ReportItemsProcessedAccurately()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(10);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();

		var progress = new Progress<DocumentProgress>(p => progressReports.Add(p));

		// Act
		await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Assert - items processed should eventually equal total
		progressReports.Last().ItemsProcessed.ShouldBe(10);
		progressReports.Last().TotalItems.ShouldBe(10);
	}

	[Fact]
	public async Task ThrowWhenNoHandlerRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(10);
		var context = CreateTestContext(provider);
		var progress = new Progress<DocumentProgress>(_ => { });

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);
		});

		exception.Message.ShouldContain("IProgressDocumentHandler");
	}

	[Fact]
	public async Task ThrowWhenDocumentIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = CreateTestContext(provider);
		var progress = new Progress<DocumentProgress>(_ => { });

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await dispatcher.DispatchWithProgressAsync<TestProgressDocument>(null!, context, progress, CancellationToken.None);
		});

		exception.ParamName.ShouldBe("document");
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(10);
		var progress = new Progress<DocumentProgress>(_ => { });

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await dispatcher.DispatchWithProgressAsync(document, null!, progress, CancellationToken.None);
		});

		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public async Task ThrowWhenProgressIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(10);
		var context = CreateTestContext(provider);

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await dispatcher.DispatchWithProgressAsync(document, context, null!, CancellationToken.None);
		});

		exception.ParamName.ShouldBe("progress");
	}

	[Fact]
	public async Task CancelWhenTokenCancelled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Use a longer delay to ensure reliable cancellation detection
		var slowHandler = new TestProgressHandler { ProcessingDelayMs = 100 };
		_ = services.AddSingleton(slowHandler);
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(20);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();
		var firstProgressReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var cts = new CancellationTokenSource();

		// Cancel deterministically on first observed progress to avoid timing-based flakiness under CI load.
		var progress = new Progress<DocumentProgress>(p =>
		{
			lock (progressReports)
			{
				progressReports.Add(p);
				_ = firstProgressReceived.TrySetResult(true);
				if (progressReports.Count == 1)
				{
					cts.Cancel();
				}
			}
		});

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await dispatcher.DispatchWithProgressAsync(document, context, progress, cts.Token);
		});

		_ = await firstProgressReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

		// Should have received some progress before cancellation but not all 20 items
		lock (progressReports)
		{
			progressReports.Count.ShouldBeGreaterThan(0);
			progressReports.Count.ShouldBeLessThan(20); // Not all items should complete
		}
	}

	[Fact]
	public async Task PropagateErrorsFromProgressHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		var errorHandler = new ErrorThrowingProgressHandler { ThrowAfterItems = 5 };
		_ = services.AddSingleton(errorHandler);
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<ErrorThrowingProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(10);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();

		var progress = new Progress<DocumentProgress>(p => progressReports.Add(p));

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);
		});

		exception.Message.ShouldContain("Simulated progress handler error");

		// Should have received some progress before error
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
		progressReports.Count.ShouldBeGreaterThan(0);
		progressReports.Last().ItemsProcessed.ShouldBeLessThanOrEqualTo(5);
	}

	[Fact]
	public async Task SupportIndeterminateProgress()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<IndeterminateProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<IndeterminateProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(5);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();

		var progress = new Progress<DocumentProgress>(p => progressReports.Add(p));

		// Act
		await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Assert - should have indeterminate progress (-1) reports
		var indeterminateReports = progressReports.Where(p => p.PercentComplete == -1).ToList();
		indeterminateReports.Count.ShouldBeGreaterThan(0);
		indeterminateReports.First().TotalItems.ShouldBeNull();

		// Final report should be complete
		progressReports.Last().PercentComplete.ShouldBe(100.0);
	}

	[Fact]
	public async Task SupportMultiPhaseProgress()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<MultiPhaseProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<MultiPhaseProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Use 9 items so it divides evenly into 3 phases
		var document = new TestProgressDocument(9);
		var context = CreateTestContext(provider);
		var progressReports = new List<DocumentProgress>();

		var progress = new Progress<DocumentProgress>(p => progressReports.Add(p));

		// Act
		await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Assert - should have different phases
		var phases = progressReports.Select(p => p.CurrentPhase).Distinct().ToList();
		phases.ShouldContain("Initializing");
		phases.ShouldContain("Processing");
		phases.ShouldContain("Finalizing");
	}

	[Fact]
	public async Task PopulateContextPropertiesDuringProcessing()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		_ = services.AddScoped<TestProgressHandler>();
		_ = services.AddScoped<IProgressDocumentHandler<TestProgressDocument>>(
			sp => sp.GetRequiredService<TestProgressHandler>());

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var document = new TestProgressDocument(5);
		var context = CreateTestContext(provider);
		var progress = new Progress<DocumentProgress>(_ => { });

		// Act
		await dispatcher.DispatchWithProgressAsync(document, context, progress, CancellationToken.None);

		// Assert
		context.CorrelationId.ShouldNotBeNullOrEmpty();
		context.CausationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void DocumentProgressFromItemsCalculatesPercentageCorrectly()
	{
		// Act
		var progress = DocumentProgress.FromItems(50, 100, "Half done");

		// Assert
		progress.PercentComplete.ShouldBe(50.0);
		progress.ItemsProcessed.ShouldBe(50);
		progress.TotalItems.ShouldBe(100);
		progress.CurrentPhase.ShouldBe("Half done");
	}

	[Fact]
	public void DocumentProgressCompletedSets100Percent()
	{
		// Act
		var progress = DocumentProgress.Completed(100, "Done");

		// Assert
		progress.PercentComplete.ShouldBe(100.0);
		progress.ItemsProcessed.ShouldBe(100);
		progress.TotalItems.ShouldBe(100);
		progress.CurrentPhase.ShouldBe("Done");
	}

	[Fact]
	public void DocumentProgressIndeterminateSetsNegativePercentage()
	{
		// Act
		var progress = DocumentProgress.Indeterminate(50, "Working...");

		// Assert
		progress.PercentComplete.ShouldBe(-1);
		progress.ItemsProcessed.ShouldBe(50);
		progress.TotalItems.ShouldBeNull();
		progress.CurrentPhase.ShouldBe("Working...");
	}

	[Fact]
	public void DocumentProgressFromItemsHandlesZeroTotal()
	{
		// Act
		var progress = DocumentProgress.FromItems(0, 0, "Empty");

		// Assert
		progress.PercentComplete.ShouldBe(0.0);
		progress.ItemsProcessed.ShouldBe(0);
		progress.TotalItems.ShouldBe(0);
	}

	private static IMessageContext CreateTestContext(IServiceProvider provider)
	{
		return DispatchContextInitializer.CreateDefaultContext(provider);
	}
}
