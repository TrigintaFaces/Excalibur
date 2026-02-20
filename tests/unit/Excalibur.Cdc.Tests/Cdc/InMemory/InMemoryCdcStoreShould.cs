// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryCdcStore"/>.
/// Tests the in-memory CDC store for testing scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcStoreShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new InMemoryCdcStore(null!));
	}

	[Fact]
	public void Constructor_CreatesStore_WithDefaultOptions()
	{
		// Act
		var store = new InMemoryCdcStore();

		// Assert
		_ = store.ShouldNotBeNull();
		store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public void Constructor_CreatesStore_WithProvidedOptions()
	{
		// Arrange
		var options = Options.Create(new InMemoryCdcOptions { PreserveHistory = true });

		// Act
		var store = new InMemoryCdcStore(options);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion

	#region AddChange Tests

	[Fact]
	public void AddChange_ThrowsArgumentNullException_WhenChangeIsNull()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => store.AddChange(null!));
	}

	[Fact]
	public void AddChange_AddsChangeToQueue()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		var change = InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 });

		// Act
		store.AddChange(change);

		// Assert
		store.GetPendingCount().ShouldBe(1);
	}

	[Fact]
	public void AddChange_SupportsMultipleChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));
		store.AddChange(InMemoryCdcChange.Update("dbo.Orders", new CdcDataChange { ColumnName = "Status", NewValue = "Shipped" }));
		store.AddChange(InMemoryCdcChange.Delete("dbo.Orders", new CdcDataChange { ColumnName = "Id", OldValue = 2 }));

		// Assert
		store.GetPendingCount().ShouldBe(3);
	}

	#endregion

	#region AddChanges Tests

	[Fact]
	public void AddChanges_ThrowsArgumentNullException_WhenChangesIsNull()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => store.AddChanges(null!));
	}

	[Fact]
	public void AddChanges_AddsAllChangesToQueue()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		var changes = new[]
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 3 })
		};

		// Act
		store.AddChanges(changes);

		// Assert
		store.GetPendingCount().ShouldBe(3);
	}

	[Fact]
	public void AddChanges_HandlesEmptyCollection()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act
		store.AddChanges(Array.Empty<InMemoryCdcChange>());

		// Assert
		store.GetPendingCount().ShouldBe(0);
	}

	#endregion

	#region GetPendingChanges Tests

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void GetPendingChanges_ThrowsArgumentOutOfRangeException_WhenMaxCountInvalid(int invalidMaxCount)
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => store.GetPendingChanges(invalidMaxCount));
	}

	[Fact]
	public void GetPendingChanges_ReturnsEmptyList_WhenNoChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act
		var result = store.GetPendingChanges(10);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetPendingChanges_ReturnsRequestedNumberOfChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		for (var i = 0; i < 10; i++)
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		}

		// Act
		var result = store.GetPendingChanges(5);

		// Assert
		result.Count.ShouldBe(5);
		store.GetPendingCount().ShouldBe(5);
	}

	[Fact]
	public void GetPendingChanges_ReturnsAllChanges_WhenMaxCountExceedsPending()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 }));

		// Act
		var result = store.GetPendingChanges(100);

		// Assert
		result.Count.ShouldBe(2);
		store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public void GetPendingChanges_MaintainsFifoOrder()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		for (var i = 0; i < 5; i++)
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		}

		// Act
		var result = store.GetPendingChanges(5);

		// Assert
		for (var i = 0; i < 5; i++)
		{
			result[i].Changes[0].NewValue.ShouldBe(i);
		}
	}

	#endregion

	#region MarkAsProcessed Tests

	[Fact]
	public void MarkAsProcessed_ThrowsArgumentNullException_WhenChangesIsNull()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => store.MarkAsProcessed(null!));
	}

	[Fact]
	public void MarkAsProcessed_PreservesHistory_WhenEnabled()
	{
		// Arrange
		var store = new InMemoryCdcStore(Options.Create(new InMemoryCdcOptions { PreserveHistory = true }));
		var changes = new[]
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};

		// Act
		store.MarkAsProcessed(changes);

		// Assert
		store.GetHistory().Count.ShouldBe(2);
	}

	[Fact]
	public void MarkAsProcessed_DoesNotPreserveHistory_WhenDisabled()
	{
		// Arrange
		var store = new InMemoryCdcStore(Options.Create(new InMemoryCdcOptions { PreserveHistory = false }));
		var changes = new[]
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};

		// Act
		store.MarkAsProcessed(changes);

		// Assert
		store.GetHistory().ShouldBeEmpty();
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void Clear_RemovesAllPendingChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		for (var i = 0; i < 5; i++)
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		}

		// Act
		store.Clear();

		// Assert
		store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public void Clear_ClearsHistory()
	{
		// Arrange
		var store = new InMemoryCdcStore(Options.Create(new InMemoryCdcOptions { PreserveHistory = true }));
		var changes = new[]
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 })
		};
		store.MarkAsProcessed(changes);

		// Act
		store.Clear();

		// Assert
		store.GetHistory().ShouldBeEmpty();
	}

	#endregion

	#region GetPendingCount Tests

	[Fact]
	public void GetPendingCount_ReturnsZero_WhenEmpty()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act
		var count = store.GetPendingCount();

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public void GetPendingCount_ReturnsCorrectCount()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }));
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 }));
		store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 3 }));

		// Act
		var count = store.GetPendingCount();

		// Assert
		count.ShouldBe(3);
	}

	#endregion

	#region GetHistory Tests

	[Fact]
	public void GetHistory_ReturnsEmptyList_WhenNoProcessedChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore();

		// Act
		var history = store.GetHistory();

		// Assert
		history.ShouldBeEmpty();
	}

	[Fact]
	public void GetHistory_ReturnsProcessedChanges()
	{
		// Arrange
		var store = new InMemoryCdcStore(Options.Create(new InMemoryCdcOptions { PreserveHistory = true }));
		var changes = new[]
		{
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 1 }),
			InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = 2 })
		};
		store.MarkAsProcessed(changes);

		// Act
		var history = store.GetHistory();

		// Assert
		history.Count.ShouldBe(2);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public void Store_IsThreadSafe_ForConcurrentAdds()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		const int totalChanges = 1000;

		// Act
		Parallel.For(0, totalChanges, i =>
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		});

		// Assert
		store.GetPendingCount().ShouldBe(totalChanges);
	}

	[Fact]
	public void Store_IsThreadSafe_ForConcurrentGets()
	{
		// Arrange
		var store = new InMemoryCdcStore();
		for (var i = 0; i < 1000; i++)
		{
			store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = i }));
		}

		var totalRetrieved = 0;

		// Act
		Parallel.For(0, 100, _ =>
		{
			var batch = store.GetPendingChanges(10);
			_ = Interlocked.Add(ref totalRetrieved, batch.Count);
		});

		// Assert - all changes should be retrieved across threads
		totalRetrieved.ShouldBe(1000);
		store.GetPendingCount().ShouldBe(0);
	}

	#endregion
}
