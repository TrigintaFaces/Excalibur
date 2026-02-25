// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryCdcProcessor"/>.
/// Tests the in-memory CDC processor for testing scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcProcessorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStoreIsNull()
	{
		// Arrange
		var options = Options.Create(new InMemoryCdcOptions());
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemoryCdcProcessor(null!, options, logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemoryCdcProcessor(store, null!, logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var options = Options.Create(new InMemoryCdcOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemoryCdcProcessor(store, options, null!));
	}

	[Fact]
	public void Constructor_CreatesProcessor_WithValidParameters()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var options = Options.Create(new InMemoryCdcOptions());
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		// Act
		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Assert
		_ = processor.ShouldNotBeNull();
	}

	#endregion

	#region ProcessChangesAsync Tests

	[Fact]
	public async Task ProcessChangesAsync_ThrowsArgumentNullException_WhenHandlerIsNull()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var options = Options.Create(new InMemoryCdcOptions());
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await processor.ProcessChangesAsync(null!, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task ProcessChangesAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var options = Options.Create(new InMemoryCdcOptions());
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		var processor = new InMemoryCdcProcessor(store, options, logger);
		processor.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await processor.ProcessChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task ProcessChangesAsync_ReturnsZero_WhenNoPendingChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act
		var result = await processor.ProcessChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task ProcessChangesAsync_ProcessesAllChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));
		store.AddChange(InMemoryCdcChange.Update("dbo.Orders", new CdcDataChange { ColumnName = "Status", OldValue = "Pending", NewValue = "Shipped" }));
		store.AddChange(InMemoryCdcChange.Delete("dbo.Orders", new CdcDataChange { ColumnName = "Id", OldValue = 2 }));

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);

		var processedChanges = new List<InMemoryCdcChange>();

		// Act
		var result = await processor.ProcessChangesAsync((change, _) =>
		{
			processedChanges.Add(change);
			return Task.CompletedTask;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(3);
		processedChanges.Count.ShouldBe(3);
	}

	[Fact]
	public async Task ProcessChangesAsync_ProcessesInBatches()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		for (var i = 0; i < 5; i++)
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		}

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 2 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act
		var result = await processor.ProcessChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(5);
		store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task ProcessChangesAsync_StopsOnCancellation()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		for (var i = 0; i < 10; i++)
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		}

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 5 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);
		using var cts = new CancellationTokenSource();

		var processedCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await processor.ProcessChangesAsync((_, ct) =>
			{
				processedCount++;
				if (processedCount == 3)
				{
					cts.Cancel();
				}

				ct.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			}, cts.Token).ConfigureAwait(false));

		processedCount.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task ProcessChangesAsync_PropagatesHandlerException()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await processor.ProcessChangesAsync((_, _) =>
				throw new InvalidOperationException("Test exception"), CancellationToken.None).ConfigureAwait(false));

		ex.Message.ShouldBe("Test exception");
	}

	[Fact]
	public async Task ProcessChangesAsync_MarksChangesAsProcessed()
	{
		// Arrange
		var store = new InMemoryCdcStore(Options.Create(new InMemoryCdcOptions { PreserveHistory = true }));
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 }));

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10, PreserveHistory = true });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act
		_ = await processor.ProcessChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

		// Assert
		store.GetPendingCount().ShouldBe(0);
		store.GetHistory().Count.ShouldBe(2);
	}

	[Fact]
	public async Task ProcessChangesAsync_CallsMarkAsProcessedOnlyAfterSuccessfulBatch()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var batch = new List<InMemoryCdcChange>
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};
		var getCount = 0;
		_ = A.CallTo(() => store.GetPendingChanges(A<int>._))
			.ReturnsLazily(() => Interlocked.Increment(ref getCount) == 1 ? batch : new List<InMemoryCdcChange>());

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();
		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act
		var result = await processor.ProcessChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(2);
		A.CallTo(() => store.MarkAsProcessed(A<IEnumerable<InMemoryCdcChange>>.That.Matches(changes =>
				changes.Count() == 2)))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessChangesAsync_DoesNotCallMarkAsProcessed_WhenHandlerThrows()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var batch = new List<InMemoryCdcChange>
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};
		_ = A.CallTo(() => store.GetPendingChanges(A<int>._)).Returns(batch);

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();
		using var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await processor.ProcessChangesAsync((_, _) =>
				throw new InvalidOperationException("boom"), CancellationToken.None).ConfigureAwait(false));

		A.CallTo(() => store.MarkAsProcessed(A<IEnumerable<InMemoryCdcChange>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ProcessChangesAsync_MarksOnlyCompletedBatches_WhenLaterBatchHandlerFails()
	{
		// Arrange
		var firstBatch = new List<InMemoryCdcChange>
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};
		var secondBatch = new List<InMemoryCdcChange>
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 3 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 4 })
		};
		var store = A.Fake<IInMemoryCdcStore>();
		var getPendingCalls = 0;
		_ = A.CallTo(() => store.GetPendingChanges(A<int>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref getPendingCalls);
				return current switch
				{
					1 => firstBatch,
					2 => secondBatch,
					_ => []
				};
			});

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 2 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();
		using var processor = new InMemoryCdcProcessor(store, options, logger);

		var handled = 0;

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			processor.ProcessChangesAsync((_, _) =>
			{
				if (Interlocked.Increment(ref handled) == 3)
				{
					throw new InvalidOperationException("second batch failed");
				}

				return Task.CompletedTask;
			}, CancellationToken.None));

		ex.Message.ShouldBe("second batch failed");
		A.CallTo(() => store.MarkAsProcessed(A<IEnumerable<InMemoryCdcChange>>.That.Matches(changes =>
				changes.Count() == firstBatch.Count &&
				changes.All(change => firstBatch.Contains(change)))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessChangesAsync_DoesNotCallMarkAsProcessed_WhenCancellationOccursInHandler()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var batch = new List<InMemoryCdcChange>
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};
		_ = A.CallTo(() => store.GetPendingChanges(A<int>._)).Returns(batch);

		var options = Options.Create(new InMemoryCdcOptions { BatchSize = 10 });
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();
		using var processor = new InMemoryCdcProcessor(store, options, logger);
		using var cts = new CancellationTokenSource();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await processor.ProcessChangesAsync((_, ct) =>
			{
				cts.Cancel();
				ct.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			}, cts.Token).ConfigureAwait(false));

		A.CallTo(() => store.MarkAsProcessed(A<IEnumerable<InMemoryCdcChange>>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var options = Options.Create(new InMemoryCdcOptions());
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act & Assert - should not throw
		processor.Dispose();
		processor.Dispose();
		processor.Dispose();
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = A.Fake<IInMemoryCdcStore>();
		var options = Options.Create(new InMemoryCdcOptions());
		var logger = A.Fake<ILogger<InMemoryCdcProcessor>>();

		var processor = new InMemoryCdcProcessor(store, options, logger);

		// Act & Assert - should not throw
		await processor.DisposeAsync().ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);
	}

	#endregion
}
