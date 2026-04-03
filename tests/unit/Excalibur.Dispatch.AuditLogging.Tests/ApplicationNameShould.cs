// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Tests;

/// <summary>
/// T.6 (wnhocr): Conformance tests for ApplicationName propagation
/// through audit store, hasher, and query filtering.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ApplicationNameShould
{
	[Fact]
	public async Task BeStoredAndRetrievedFromInMemoryStore()
	{
		var store = new InMemoryAuditStore();
		var auditEvent = CreateEvent("app-1");

		await store.StoreAsync(auditEvent, CancellationToken.None);

		var retrieved = await store.GetByIdAsync(auditEvent.EventId, CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.ApplicationName.ShouldBe("app-1");
	}

	[Fact]
	public async Task FilterByApplicationNameInQuery()
	{
		var store = new InMemoryAuditStore();
		await store.StoreAsync(CreateEvent("app-a"), CancellationToken.None);
		await store.StoreAsync(CreateEvent("app-b"), CancellationToken.None);
		await store.StoreAsync(CreateEvent("app-a"), CancellationToken.None);

		var query = new AuditQuery { ApplicationName = "app-a" };
		var results = await store.QueryAsync(query, CancellationToken.None);

		results.Count.ShouldBe(2);
		results.ShouldAllBe(e => e.ApplicationName == "app-a");
	}

	[Fact]
	public async Task ReturnAllWhenApplicationNameFilterIsNull()
	{
		var store = new InMemoryAuditStore();
		await store.StoreAsync(CreateEvent("app-x"), CancellationToken.None);
		await store.StoreAsync(CreateEvent("app-y"), CancellationToken.None);

		var query = new AuditQuery { ApplicationName = null };
		var results = await store.QueryAsync(query, CancellationToken.None);

		results.Count.ShouldBe(2);
	}

	[Fact]
	public void BeIncludedInAuditHash()
	{
		var event1 = CreateEvent("app-1");
		var event2 = CreateEvent("app-2");

		// Different application names should produce different hashes
		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ProduceSameHashForSameApplicationName()
	{
		var event1 = CreateEvent("same-app");
		// Create identical event
		var event2 = new AuditEvent
		{
			EventId = event1.EventId,
			EventType = event1.EventType,
			Action = event1.Action,
			Outcome = event1.Outcome,
			Timestamp = event1.Timestamp,
			ActorId = event1.ActorId,
			ApplicationName = "same-app"
		};

		var hash1 = AuditHasher.ComputeHash(event1, null);
		var hash2 = AuditHasher.ComputeHash(event2, null);

		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void AllowNullApplicationName()
	{
		var evt = new AuditEvent
		{
			EventId = Guid.NewGuid().ToString(),
			EventType = AuditEventType.DataAccess,
			Action = "Test",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			ApplicationName = null
		};

		evt.ApplicationName.ShouldBeNull();

		// Should not throw when hashing with null ApplicationName
		var hash = AuditHasher.ComputeHash(evt, null);
		hash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task SupportEmptyStringApplicationName()
	{
		var store = new InMemoryAuditStore();
		var evt = CreateEvent("");

		await store.StoreAsync(evt, CancellationToken.None);

		var retrieved = await store.GetByIdAsync(evt.EventId, CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.ApplicationName.ShouldBe("");
	}

	private static AuditEvent CreateEvent(string? applicationName) => new()
	{
		EventId = Guid.NewGuid().ToString(),
		EventType = AuditEventType.DataAccess,
		Action = "Test",
		Outcome = AuditOutcome.Success,
		Timestamp = DateTimeOffset.UtcNow,
		ActorId = "user-1",
		ApplicationName = applicationName
	};
}
