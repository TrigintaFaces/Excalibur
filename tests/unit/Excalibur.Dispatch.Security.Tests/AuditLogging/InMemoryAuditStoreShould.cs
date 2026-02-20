// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[UnitTest]
public sealed class InMemoryAuditStoreShould : IDisposable
{
	private readonly InMemoryAuditStore _store;

	public InMemoryAuditStoreShould()
	{
		_store = new InMemoryAuditStore();
	}

	public void Dispose() => _store.Clear();

	#region StoreAsync Tests

	[Fact]
	public async Task StoreAsync_AssignsHashAndSequenceNumber()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent("event-1");

		// Act
		var result = await _store.StoreAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("event-1");
		result.EventHash.ShouldNotBeNullOrWhiteSpace();
		result.SequenceNumber.ShouldBe(1);
		result.RecordedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task StoreAsync_LinksHashChain()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1");
		var event2 = CreateTestAuditEvent("event-2");

		// Act
		var result1 = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Assert
		var storedEvent1 = await _store.GetByIdAsync("event-1", CancellationToken.None);
		var storedEvent2 = await _store.GetByIdAsync("event-2", CancellationToken.None);

		storedEvent1.EventHash.ShouldBe(result1.EventHash);
		storedEvent2.PreviousEventHash.ShouldBe(result1.EventHash);
	}

	[Fact]
	public async Task StoreAsync_ThrowsOnDuplicateEventId()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("duplicate-id");
		var event2 = CreateTestAuditEvent("duplicate-id");

		// Act
		_ = await _store.StoreAsync(event1, CancellationToken.None);

		// Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() => _store.StoreAsync(event2, CancellationToken.None));
	}

	[Fact]
	public async Task StoreAsync_ThrowsOnNullAuditEvent()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => _store.StoreAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task StoreAsync_ThrowsOnCancellation()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent("event-1");
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_store.StoreAsync(auditEvent, cts.Token));
	}

	[Fact]
	public async Task StoreAsync_IncrementsSequenceNumberAcrossEvents()
	{
		// Arrange & Act
		var result1 = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		var result2 = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);
		var result3 = await _store.StoreAsync(CreateTestAuditEvent("event-3"), CancellationToken.None);

		// Assert
		result1.SequenceNumber.ShouldBe(1);
		result2.SequenceNumber.ShouldBe(2);
		result3.SequenceNumber.ShouldBe(3);
	}

	[Fact]
	public async Task StoreAsync_IsolatesHashChainsByTenant()
	{
		// Arrange
		var eventA = CreateTestAuditEvent("event-a") with { TenantId = "tenant-a" };
		var eventB = CreateTestAuditEvent("event-b") with { TenantId = "tenant-b" };
		var eventA2 = CreateTestAuditEvent("event-a2") with { TenantId = "tenant-a" };

		// Act
		_ = await _store.StoreAsync(eventA, CancellationToken.None);
		_ = await _store.StoreAsync(eventB, CancellationToken.None);
		_ = await _store.StoreAsync(eventA2, CancellationToken.None);

		// Assert
		var storedA = await _store.GetByIdAsync("event-a", CancellationToken.None);
		var storedB = await _store.GetByIdAsync("event-b", CancellationToken.None);
		var storedA2 = await _store.GetByIdAsync("event-a2", CancellationToken.None);

		// Tenant A chain: event-a -> event-a2
		storedA2.PreviousEventHash.ShouldBe(storedA.EventHash);
		// Tenant B has its own genesis hash, not linked to Tenant A
		storedB.PreviousEventHash.ShouldNotBe(storedA.EventHash);
	}

	[Fact]
	public async Task StoreAsync_UpdatesCountProperty()
	{
		// Arrange & Act
		_store.Count.ShouldBe(0);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);

		// Assert
		_store.Count.ShouldBe(1);

		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);
		_store.Count.ShouldBe(2);
	}

	[Fact]
	public async Task StoreAsync_DefaultTenantUsesDefaultKey()
	{
		// Arrange - event with null TenantId goes to _default_ key
		var event1 = CreateTestAuditEvent("default-1");
		var event2 = CreateTestAuditEvent("default-2");

		// Act
		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Assert - both events should be in the default tenant chain
		var storedEvent2 = await _store.GetByIdAsync("default-2", CancellationToken.None);
		var storedEvent1 = await _store.GetByIdAsync("default-1", CancellationToken.None);
		storedEvent2.PreviousEventHash.ShouldBe(storedEvent1.EventHash);
	}

	[Fact]
	public async Task StoreAsync_FirstEventHasGenesisHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent("first-event");

		// Act
		_ = await _store.StoreAsync(auditEvent, CancellationToken.None);

		// Assert - first event should have a previous hash (genesis hash)
		var stored = await _store.GetByIdAsync("first-event", CancellationToken.None);
		stored.PreviousEventHash.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task StoreAsync_TenantFirstEventHasGenesisHash()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent("tenant-first") with { TenantId = "my-tenant" };

		// Act
		_ = await _store.StoreAsync(auditEvent, CancellationToken.None);

		// Assert - first event in tenant chain should have a genesis hash
		var stored = await _store.GetByIdAsync("tenant-first", CancellationToken.None);
		stored.PreviousEventHash.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task StoreAsync_DuplicateEventIdMessageContainsEventId()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("dup-id-msg"), CancellationToken.None);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _store.StoreAsync(CreateTestAuditEvent("dup-id-msg"), CancellationToken.None));
		ex.Message.ShouldContain("dup-id-msg");
	}

	[Fact]
	public async Task StoreAsync_MultipleTenantsShareSequenceNumber()
	{
		// Arrange - sequence number is global, not per-tenant
		var eventA = CreateTestAuditEvent("event-a") with { TenantId = "tenant-a" };
		var eventB = CreateTestAuditEvent("event-b") with { TenantId = "tenant-b" };

		// Act
		var resultA = await _store.StoreAsync(eventA, CancellationToken.None);
		var resultB = await _store.StoreAsync(eventB, CancellationToken.None);

		// Assert - sequence numbers are global across tenants
		resultA.SequenceNumber.ShouldBe(1);
		resultB.SequenceNumber.ShouldBe(2);
	}

	[Fact]
	public async Task StoreAsync_EventHashDiffersAcrossEvents()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("hash-diff-1") with { Action = "Create" };
		var event2 = CreateTestAuditEvent("hash-diff-2") with { Action = "Delete" };

		// Act
		var result1 = await _store.StoreAsync(event1, CancellationToken.None);
		var result2 = await _store.StoreAsync(event2, CancellationToken.None);

		// Assert - different events should have different hashes
		result1.EventHash.ShouldNotBe(result2.EventHash);
	}

	#endregion StoreAsync Tests

	#region GetByIdAsync Tests

	[Fact]
	public async Task GetByIdAsync_ReturnsStoredEvent()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent("event-1");
		_ = await _store.StoreAsync(auditEvent, CancellationToken.None);

		// Act
		var retrieved = await _store.GetByIdAsync("event-1", CancellationToken.None);

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.EventId.ShouldBe("event-1");
		retrieved.Action.ShouldBe("Read");
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsNullForNonExistent()
	{
		// Act
		var retrieved = await _store.GetByIdAsync("non-existent", CancellationToken.None);

		// Assert
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsOnNullOrWhitespaceEventId()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _store.GetByIdAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => _store.GetByIdAsync("", CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(() => _store.GetByIdAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsOnCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_store.GetByIdAsync("some-id", cts.Token));
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsEventWithHashAndChainInfo()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent("event-with-hash");
		_ = await _store.StoreAsync(auditEvent, CancellationToken.None);

		// Act
		var retrieved = await _store.GetByIdAsync("event-with-hash", CancellationToken.None);

		// Assert
		_ = retrieved.ShouldNotBeNull();
		retrieved.EventHash.ShouldNotBeNullOrWhiteSpace();
		retrieved.PreviousEventHash.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion GetByIdAsync Tests

	#region QueryAsync Tests

	[Fact]
	public async Task QueryAsync_FiltersByDateRange()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)
		};
		var event2 = CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero)
		};
		var event3 = CreateTestAuditEvent("event-3") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			StartDate = new DateTimeOffset(2025, 1, 12, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2025, 1, 18, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("event-2");
	}

	[Fact]
	public async Task QueryAsync_FiltersByStartDateOnly()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)
		};
		var event2 = CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			StartDate = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("event-2");
	}

	[Fact]
	public async Task QueryAsync_FiltersByEndDateOnly()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)
		};
		var event2 = CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			EndDate = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("event-1");
	}

	[Fact]
	public async Task QueryAsync_FiltersByEventType()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { EventType = AuditEventType.DataAccess };
		var event2 = CreateTestAuditEvent("event-2") with { EventType = AuditEventType.Authentication };
		var event3 = CreateTestAuditEvent("event-3") with { EventType = AuditEventType.DataAccess };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			EventTypes = [AuditEventType.DataAccess]
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(e => e.EventType == AuditEventType.DataAccess);
	}

	[Fact]
	public async Task QueryAsync_FiltersByActorId()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { ActorId = "user-1" };
		var event2 = CreateTestAuditEvent("event-2") with { ActorId = "user-2" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { ActorId = "user-1" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].ActorId.ShouldBe("user-1");
	}

	[Fact]
	public async Task QueryAsync_FiltersByTenant()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" };
		var event2 = CreateTestAuditEvent("event-2") with { TenantId = "tenant-b" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-a" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].TenantId.ShouldBe("tenant-a");
	}

	[Fact]
	public async Task QueryAsync_SupportsPagination()
	{
		// Arrange
		for (var i = 1; i <= 10; i++)
		{
			_ = await _store.StoreAsync(CreateTestAuditEvent($"event-{i}") with
			{
				Timestamp = DateTimeOffset.UtcNow.AddMinutes(i)
			}, CancellationToken.None);
		}

		// Act
		var page1 = await _store.QueryAsync(new AuditQuery { MaxResults = 3, Skip = 0, OrderByDescending = false }, CancellationToken.None);
		var page2 = await _store.QueryAsync(new AuditQuery { MaxResults = 3, Skip = 3, OrderByDescending = false }, CancellationToken.None);

		// Assert
		page1.Count.ShouldBe(3);
		page2.Count.ShouldBe(3);
		page1.Select(e => e.EventId).ShouldNotBe(page2.Select(e => e.EventId));
	}

	[Fact]
	public async Task QueryAsync_FiltersByOutcome()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { Outcome = AuditOutcome.Success };
		var event2 = CreateTestAuditEvent("event-2") with { Outcome = AuditOutcome.Failure };
		var event3 = CreateTestAuditEvent("event-3") with { Outcome = AuditOutcome.Success };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			Outcomes = [AuditOutcome.Failure]
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("event-2");
	}

	[Fact]
	public async Task QueryAsync_FiltersByResourceId()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { ResourceId = "res-1" };
		var event2 = CreateTestAuditEvent("event-2") with { ResourceId = "res-2" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { ResourceId = "res-1" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].ResourceId.ShouldBe("res-1");
	}

	[Fact]
	public async Task QueryAsync_FiltersByResourceType()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { ResourceType = "Customer" };
		var event2 = CreateTestAuditEvent("event-2") with { ResourceType = "Order" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { ResourceType = "Customer" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].ResourceType.ShouldBe("Customer");
	}

	[Fact]
	public async Task QueryAsync_FiltersByMinimumClassification()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { ResourceClassification = DataClassification.Public };
		var event2 = CreateTestAuditEvent("event-2") with { ResourceClassification = DataClassification.Confidential };
		var event3 = CreateTestAuditEvent("event-3") with { ResourceClassification = DataClassification.Restricted };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			MinimumClassification = DataClassification.Confidential
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(e => e.ResourceClassification >= DataClassification.Confidential);
	}

	[Fact]
	public async Task QueryAsync_FiltersByCorrelationId()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { CorrelationId = "corr-1" };
		var event2 = CreateTestAuditEvent("event-2") with { CorrelationId = "corr-2" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { CorrelationId = "corr-1" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].CorrelationId.ShouldBe("corr-1");
	}

	[Fact]
	public async Task QueryAsync_FiltersByAction()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { Action = "Create" };
		var event2 = CreateTestAuditEvent("event-2") with { Action = "Delete" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { Action = "Create" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].Action.ShouldBe("Create");
	}

	[Fact]
	public async Task QueryAsync_FiltersByIpAddress()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { IpAddress = "10.0.0.1" };
		var event2 = CreateTestAuditEvent("event-2") with { IpAddress = "10.0.0.2" };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { IpAddress = "10.0.0.1" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].IpAddress.ShouldBe("10.0.0.1");
	}

	[Fact]
	public async Task QueryAsync_OrdersByTimestampDescendingByDefault()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)
		};
		var event2 = CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero)
		};
		var event3 = CreateTestAuditEvent("event-3") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 12, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act - default is OrderByDescending = true
		var results = await _store.QueryAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		results[0].EventId.ShouldBe("event-2"); // newest first
		results[1].EventId.ShouldBe("event-3");
		results[2].EventId.ShouldBe("event-1"); // oldest last
	}

	[Fact]
	public async Task QueryAsync_OrdersByTimestampAscending()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)
		};
		var event2 = CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { OrderByDescending = false }, CancellationToken.None);

		// Assert
		results[0].EventId.ShouldBe("event-1"); // oldest first
		results[1].EventId.ShouldBe("event-2");
	}

	[Fact]
	public async Task QueryAsync_ReturnsEmptyForNonExistentTenant()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { TenantId = "non-existent" }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(0);
	}

	[Fact]
	public async Task QueryAsync_ThrowsOnNullQuery()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => _store.QueryAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_ThrowsOnCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_store.QueryAsync(new AuditQuery(), cts.Token));
	}

	[Fact]
	public async Task QueryAsync_CombinesMultipleFilters()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with
		{
			ActorId = "user-1",
			EventType = AuditEventType.DataAccess,
			ResourceType = "Customer"
		};
		var event2 = CreateTestAuditEvent("event-2") with
		{
			ActorId = "user-1",
			EventType = AuditEventType.Authentication,
			ResourceType = "Customer"
		};
		var event3 = CreateTestAuditEvent("event-3") with
		{
			ActorId = "user-2",
			EventType = AuditEventType.DataAccess,
			ResourceType = "Customer"
		};

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			ActorId = "user-1",
			EventTypes = [AuditEventType.DataAccess]
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("event-1");
	}

	[Fact]
	public async Task QueryAsync_WithEmptyEventTypesFilter_ReturnsAllEvents()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { EventType = AuditEventType.DataAccess }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with { EventType = AuditEventType.Authentication }, CancellationToken.None);

		// Act - empty list should not filter
		var results = await _store.QueryAsync(new AuditQuery { EventTypes = [] }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task QueryAsync_WithEmptyOutcomesFilter_ReturnsAllEvents()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { Outcome = AuditOutcome.Success }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with { Outcome = AuditOutcome.Failure }, CancellationToken.None);

		// Act - empty list should not filter
		var results = await _store.QueryAsync(new AuditQuery { Outcomes = [] }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task QueryAsync_ReturnsAllEventsForAllTenantsWhenNoTenantSpecified()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with { TenantId = "tenant-b" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3"), CancellationToken.None); // null tenant

		// Act - no tenant filter
		var results = await _store.QueryAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
	}

	[Fact]
	public async Task QueryAsync_WithAllFiltersApplied()
	{
		// Arrange - a comprehensive event
		var targetEvent = CreateTestAuditEvent("target") with
		{
			ActorId = "actor-x",
			ResourceId = "res-x",
			ResourceType = "TypeX",
			CorrelationId = "corr-x",
			Action = "Update",
			IpAddress = "192.168.1.1",
			EventType = AuditEventType.DataModification,
			Outcome = AuditOutcome.Success,
			ResourceClassification = DataClassification.Confidential,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero)
		};

		var noiseEvent = CreateTestAuditEvent("noise") with
		{
			ActorId = "actor-y",
			ResourceId = "res-y",
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(targetEvent, CancellationToken.None);
		_ = await _store.StoreAsync(noiseEvent, CancellationToken.None);

		// Act - apply all filters to match only the target event
		var results = await _store.QueryAsync(new AuditQuery
		{
			ActorId = "actor-x",
			ResourceId = "res-x",
			ResourceType = "TypeX",
			CorrelationId = "corr-x",
			Action = "Update",
			IpAddress = "192.168.1.1",
			EventTypes = [AuditEventType.DataModification],
			Outcomes = [AuditOutcome.Success],
			MinimumClassification = DataClassification.Confidential,
			StartDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("target");
	}

	[Fact]
	public async Task QueryAsync_SkipBeyondTotalReturnsEmpty()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery { Skip = 100 }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(0);
	}

	[Fact]
	public async Task QueryAsync_MaxResultsLimitsOutput()
	{
		// Arrange
		for (var i = 1; i <= 5; i++)
		{
			_ = await _store.StoreAsync(CreateTestAuditEvent($"event-{i}"), CancellationToken.None);
		}

		// Act
		var results = await _store.QueryAsync(new AuditQuery { MaxResults = 2 }, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task QueryAsync_WithTenantFilter_AppliesAllOtherFilters()
	{
		// Arrange - verify all query filters work within a tenant-scoped query
		var matchingEvent = CreateTestAuditEvent("match") with
		{
			TenantId = "tenant-x",
			ActorId = "actor-1",
			ResourceId = "res-1",
			ResourceType = "Order",
			CorrelationId = "corr-1",
			Action = "Create",
			IpAddress = "10.0.0.1",
			EventType = AuditEventType.DataModification,
			Outcome = AuditOutcome.Success,
			ResourceClassification = DataClassification.Confidential,
			Timestamp = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero)
		};
		var nonMatchingEvent = CreateTestAuditEvent("no-match") with
		{
			TenantId = "tenant-x",
			ActorId = "actor-2",
			Timestamp = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero)
		};

		_ = await _store.StoreAsync(matchingEvent, CancellationToken.None);
		_ = await _store.StoreAsync(nonMatchingEvent, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			TenantId = "tenant-x",
			ActorId = "actor-1",
			ResourceId = "res-1",
			ResourceType = "Order",
			CorrelationId = "corr-1",
			Action = "Create",
			IpAddress = "10.0.0.1",
			EventTypes = [AuditEventType.DataModification],
			Outcomes = [AuditOutcome.Success],
			MinimumClassification = DataClassification.Confidential,
			StartDate = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2025, 3, 31, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
		results[0].EventId.ShouldBe("match");
	}

	[Fact]
	public async Task QueryAsync_WithMultipleEventTypes_FiltersCorrectly()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1") with { EventType = AuditEventType.Authentication }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2") with { EventType = AuditEventType.DataAccess }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-3") with { EventType = AuditEventType.Security }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-4") with { EventType = AuditEventType.System }, CancellationToken.None);

		// Act - filter by multiple event types
		var results = await _store.QueryAsync(new AuditQuery
		{
			EventTypes = [AuditEventType.Authentication, AuditEventType.Security]
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(e =>
			e.EventType == AuditEventType.Authentication || e.EventType == AuditEventType.Security);
	}

	[Fact]
	public async Task QueryAsync_WithMultipleOutcomes_FiltersCorrectly()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1") with { Outcome = AuditOutcome.Success }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2") with { Outcome = AuditOutcome.Failure }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-3") with { Outcome = AuditOutcome.Denied }, CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(new AuditQuery
		{
			Outcomes = [AuditOutcome.Success, AuditOutcome.Denied]
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(e => e.Outcome == AuditOutcome.Success || e.Outcome == AuditOutcome.Denied);
	}

	[Fact]
	public async Task QueryAsync_NullFiltersReturnAllEvents()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-3"), CancellationToken.None);

		// Act - all filters null (default AuditQuery)
		var results = await _store.QueryAsync(new AuditQuery
		{
			EventTypes = null,
			Outcomes = null,
			ActorId = null,
			ResourceId = null,
			ResourceType = null,
			CorrelationId = null,
			Action = null,
			IpAddress = null,
			MinimumClassification = null,
			StartDate = null,
			EndDate = null
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
	}

	#endregion QueryAsync Tests

	#region CountAsync Tests

	[Fact]
	public async Task CountAsync_ReturnsCorrectCount()
	{
		// Arrange
		var event1 = CreateTestAuditEvent("event-1") with { EventType = AuditEventType.DataAccess };
		var event2 = CreateTestAuditEvent("event-2") with { EventType = AuditEventType.Authentication };
		var event3 = CreateTestAuditEvent("event-3") with { EventType = AuditEventType.DataAccess };

		_ = await _store.StoreAsync(event1, CancellationToken.None);
		_ = await _store.StoreAsync(event2, CancellationToken.None);
		_ = await _store.StoreAsync(event3, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { EventTypes = [AuditEventType.DataAccess] }, CancellationToken.None);

		// Assert
		count.ShouldBe(2);
	}

	[Fact]
	public async Task CountAsync_ReturnsZeroForNonExistentTenant()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { TenantId = "non-existent" }, CancellationToken.None);

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public async Task CountAsync_FiltersByTenant()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with { TenantId = "tenant-b" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3") with { TenantId = "tenant-a" }, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { TenantId = "tenant-a" }, CancellationToken.None);

		// Assert
		count.ShouldBe(2);
	}

	[Fact]
	public async Task CountAsync_ThrowsOnNullQuery()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => _store.CountAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CountAsync_ThrowsOnCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_store.CountAsync(new AuditQuery(), cts.Token));
	}

	[Fact]
	public async Task CountAsync_CountsAllEventsWithEmptyQuery()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3"), CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		count.ShouldBe(3);
	}

	[Fact]
	public async Task CountAsync_AppliesAllFilters()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with
		{
			ActorId = "user-1",
			ResourceType = "Order",
			Action = "Create",
			EventType = AuditEventType.DataModification,
			Outcome = AuditOutcome.Success
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with
		{
			ActorId = "user-2",
			ResourceType = "Customer",
			Action = "Delete",
			EventType = AuditEventType.DataAccess,
			Outcome = AuditOutcome.Failure
		}, CancellationToken.None);

		// Act - filter that matches only event-1
		var count = await _store.CountAsync(new AuditQuery
		{
			ActorId = "user-1",
			ResourceType = "Order",
			Action = "Create"
		}, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task CountAsync_ReturnsZeroForEmptyStore()
	{
		// Act
		var count = await _store.CountAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public async Task CountAsync_WithTenantFilter_AppliesAllFilters()
	{
		// Arrange - verify count with tenant scope + additional filters
		_ = await _store.StoreAsync(CreateTestAuditEvent("count-1") with
		{
			TenantId = "tenant-x",
			EventType = AuditEventType.DataAccess,
			ActorId = "actor-1"
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("count-2") with
		{
			TenantId = "tenant-x",
			EventType = AuditEventType.Authentication,
			ActorId = "actor-2"
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("count-3") with
		{
			TenantId = "tenant-y",
			EventType = AuditEventType.DataAccess,
			ActorId = "actor-1"
		}, CancellationToken.None);

		// Act - count only DataAccess events for tenant-x by actor-1
		var count = await _store.CountAsync(new AuditQuery
		{
			TenantId = "tenant-x",
			EventTypes = [AuditEventType.DataAccess],
			ActorId = "actor-1"
		}, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task CountAsync_WithDateRange_ReturnsFilteredCount()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("old") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("mid") with
		{
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("new") with
		{
			Timestamp = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery
		{
			StartDate = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2025, 9, 30, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task CountAsync_WithResourceIdFilter()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1") with { ResourceId = "res-a" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2") with { ResourceId = "res-b" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-3") with { ResourceId = "res-a" }, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { ResourceId = "res-a" }, CancellationToken.None);

		// Assert
		count.ShouldBe(2);
	}

	[Fact]
	public async Task CountAsync_WithCorrelationIdFilter()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1") with { CorrelationId = "corr-abc" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2") with { CorrelationId = "corr-xyz" }, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { CorrelationId = "corr-abc" }, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task CountAsync_WithIpAddressFilter()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1") with { IpAddress = "192.168.0.1" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2") with { IpAddress = "10.0.0.1" }, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { IpAddress = "192.168.0.1" }, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task CountAsync_WithMinimumClassificationFilter()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1") with { ResourceClassification = DataClassification.Public }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2") with { ResourceClassification = DataClassification.Confidential }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-3") with { ResourceClassification = DataClassification.Restricted }, CancellationToken.None);

		// Act
		var count = await _store.CountAsync(new AuditQuery { MinimumClassification = DataClassification.Confidential }, CancellationToken.None);

		// Assert
		count.ShouldBe(2);
	}

	#endregion CountAsync Tests

	#region VerifyChainIntegrityAsync Tests

	[Fact]
	public async Task VerifyChainIntegrityAsync_ReturnsValidForIntactChain()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 11, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 12, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Act
		var result = await _store.VerifyChainIntegrityAsync(
			new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(3);
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_ReturnsValidForEmptyRange()
	{
		// Act
		var result = await _store.VerifyChainIntegrityAsync(
			new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(0);
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_ThrowsOnCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_store.VerifyChainIntegrityAsync(
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow,
				cts.Token));
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_OnlyVerifiesEventsInRange()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 5, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3") with
		{
			Timestamp = new DateTimeOffset(2025, 1, 25, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Act - only query for middle event
		var result = await _store.VerifyChainIntegrityAsync(
			new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(1);
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_ReturnsCorrectDateRange()
	{
		// Arrange
		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);

		// Act
		var result = await _store.VerifyChainIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		result.StartDate.ShouldBe(startDate);
		result.EndDate.ShouldBe(endDate);
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_SingleEventVerifiesSuccessfully()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("solo-event") with
		{
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Act
		var result = await _store.VerifyChainIntegrityAsync(
			new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(1);
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_MultipleTenantsVerifyIndependently()
	{
		// Arrange - events from different tenants
		_ = await _store.StoreAsync(CreateTestAuditEvent("a-1") with
		{
			TenantId = "tenant-a",
			Timestamp = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("b-1") with
		{
			TenantId = "tenant-b",
			Timestamp = new DateTimeOffset(2025, 6, 2, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Act
		var result = await _store.VerifyChainIntegrityAsync(
			new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		// Assert - all events should verify (each has its own chain hash)
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(2);
	}

	#endregion VerifyChainIntegrityAsync Tests

	#region GetLastEventAsync Tests

	[Fact]
	public async Task GetLastEventAsync_ReturnsLastStoredEvent()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3"), CancellationToken.None);

		// Act
		var lastEvent = await _store.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		_ = lastEvent.ShouldNotBeNull();
		lastEvent.EventId.ShouldBe("event-3");
	}

	[Fact]
	public async Task GetLastEventAsync_ReturnsNullForEmptyStore()
	{
		// Act
		var lastEvent = await _store.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		lastEvent.ShouldBeNull();
	}

	[Fact]
	public async Task GetLastEventAsync_FiltersbyTenant()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with { TenantId = "tenant-b" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-3") with { TenantId = "tenant-a" }, CancellationToken.None);

		// Act
		var lastEventA = await _store.GetLastEventAsync("tenant-a", CancellationToken.None);
		var lastEventB = await _store.GetLastEventAsync("tenant-b", CancellationToken.None);

		// Assert
		lastEventA.EventId.ShouldBe("event-3");
		lastEventB.EventId.ShouldBe("event-2");
	}

	[Fact]
	public async Task GetLastEventAsync_ReturnsNullForNonExistentTenant()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);

		// Act
		var lastEvent = await _store.GetLastEventAsync("non-existent", CancellationToken.None);

		// Assert
		lastEvent.ShouldBeNull();
	}

	[Fact]
	public async Task GetLastEventAsync_ThrowsOnCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_store.GetLastEventAsync(null, cts.Token));
	}

	[Fact]
	public async Task GetLastEventAsync_WithNullTenantIdReturnsDefaultTenantEvent()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("default-event"), cancellationToken: CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("tenant-event") with { TenantId = "some-tenant" }, CancellationToken.None);

		// Act - null tenant should return default tenant events
		var lastEvent = await _store.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		_ = lastEvent.ShouldNotBeNull();
		lastEvent.EventId.ShouldBe("default-event");
	}

	#endregion GetLastEventAsync Tests

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllEvents()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);

		// Act
		_store.Clear();

		// Assert
		_store.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Clear_ResetsSequenceNumber()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2"), CancellationToken.None);

		// Act
		_store.Clear();
		var result = await _store.StoreAsync(CreateTestAuditEvent("event-3"), CancellationToken.None);

		// Assert
		result.SequenceNumber.ShouldBe(1);
	}

	[Fact]
	public async Task Clear_AllowsNewEventsWithSameIds()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);
		_store.Clear();

		// Act - should not throw even though same ID was used before
		var result = await _store.StoreAsync(CreateTestAuditEvent("event-1"), CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("event-1");
		_store.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Clear_ClearsTenantEvents()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-1") with { TenantId = "tenant-a" }, CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("event-2") with { TenantId = "tenant-b" }, CancellationToken.None);

		// Act
		_store.Clear();

		// Assert - queries should return empty for all tenants
		var resultsA = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-a" }, CancellationToken.None);
		var resultsB = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-b" }, CancellationToken.None);
		var lastEventA = await _store.GetLastEventAsync("tenant-a", CancellationToken.None);
		var lastEventB = await _store.GetLastEventAsync("tenant-b", CancellationToken.None);

		resultsA.Count.ShouldBe(0);
		resultsB.Count.ShouldBe(0);
		lastEventA.ShouldBeNull();
		lastEventB.ShouldBeNull();
	}

	[Fact]
	public void Clear_WorksOnEmptyStore()
	{
		// Act - should not throw
		_store.Clear();

		// Assert
		_store.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Clear_ResetsGetByIdLookup()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-clear"), CancellationToken.None);

		// Act
		_store.Clear();
		var result = await _store.GetByIdAsync("evt-clear", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task Clear_ResetsCountAsync()
	{
		// Arrange
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-1"), CancellationToken.None);
		_ = await _store.StoreAsync(CreateTestAuditEvent("evt-2"), CancellationToken.None);

		// Act
		_store.Clear();
		var count = await _store.CountAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		count.ShouldBe(0);
	}

	#endregion Clear Tests

	private static AuditEvent CreateTestAuditEvent(string eventId) =>
		new()
		{
			EventId = eventId,
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-123",
			TenantId = null
		};
}
