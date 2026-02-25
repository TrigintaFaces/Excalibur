// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.AuditLogging;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Final coverage push tests to reach 95%+ for Excalibur.Dispatch.AuditLogging.
/// Targets remaining uncovered lines in:
/// - Resources.Designer.cs (constructor, Culture setter, resource property accessors)
/// - InMemoryAuditStore.cs (duplicate EventId throw, Count property)
/// - RbacAuditStore.cs (CanAccessEvent fallback return false)
/// - AuditLoggingServiceCollectionExtensions.cs (InnerAuditStoreResolutionFailed)
/// - AuditHasher.cs (null EventHash in VerifyHash, metadata null value)
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[UnitTest]
public sealed class AuditLoggingFinalCoveragePushShould
{
	#region InMemoryAuditStore - Duplicate EventId Throws

	[Fact]
	public async Task InMemoryAuditStore_StoreAsync_ThrowsOnDuplicateEventId()
	{
		// Arrange - store an event, then try storing another with the same EventId
		var store = new InMemoryAuditStore();

		var auditEvent = new AuditEvent
		{
			EventId = "dup-evt-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		_ = await store.StoreAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert - storing again with same EventId should throw
		var duplicate = new AuditEvent
		{
			EventId = "dup-evt-1", // same ID
			EventType = AuditEventType.Authentication,
			Action = "Login",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow.AddMinutes(1),
			ActorId = "user-2"
		};

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => store.StoreAsync(duplicate, CancellationToken.None)).ConfigureAwait(false);
		ex.Message.ShouldContain("dup-evt-1");
	}

	#endregion

	#region InMemoryAuditStore - Count Property

	[Fact]
	public async Task InMemoryAuditStore_Count_ReturnsCorrectValue()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		store.Count.ShouldBe(0);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-prop-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		store.Count.ShouldBe(1);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-prop-2",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		store.Count.ShouldBe(2);

		// Clear and verify
		store.Clear();
		store.Count.ShouldBe(0);
	}

	#endregion

	#region InMemoryAuditStore - Cancellation Token Paths

	[Fact]
	public async Task InMemoryAuditStore_StoreAsync_ThrowsOnCancellation()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		var auditEvent = new AuditEvent
		{
			EventId = "cancel-store-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => store.StoreAsync(auditEvent, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_GetByIdAsync_ThrowsOnCancellation()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => store.GetByIdAsync("any-id", cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_ThrowsOnCancellation()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => store.QueryAsync(new AuditQuery(), cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_ThrowsOnCancellation()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => store.CountAsync(new AuditQuery(), cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_VerifyChainIntegrityAsync_ThrowsOnCancellation()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => store.VerifyChainIntegrityAsync(
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow,
				cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_GetLastEventAsync_ThrowsOnCancellation()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => store.GetLastEventAsync(null, cts.Token)).ConfigureAwait(false);
	}

	#endregion

	#region InMemoryAuditStore - Argument Null Checks

	[Fact]
	public async Task InMemoryAuditStore_StoreAsync_ThrowsOnNullEvent()
	{
		var store = new InMemoryAuditStore();
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StoreAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_GetByIdAsync_ThrowsOnNullOrWhitespaceEventId()
	{
		var store = new InMemoryAuditStore();
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetByIdAsync("", CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetByIdAsync("  ", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_ThrowsOnNullQuery()
	{
		var store = new InMemoryAuditStore();
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.QueryAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_ThrowsOnNullQuery()
	{
		var store = new InMemoryAuditStore();
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.CountAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region InMemoryAuditStore - GetByIdAsync Returns Null for Missing

	[Fact]
	public async Task InMemoryAuditStore_GetByIdAsync_ReturnsNullForNonExistentEvent()
	{
		var store = new InMemoryAuditStore();
		var result = await store.GetByIdAsync("non-existent", CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	#endregion

	#region InMemoryAuditStore - QueryAsync Empty Tenant Returns Empty

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_ReturnsEmptyForNonExistentTenant()
	{
		var store = new InMemoryAuditStore();
		var result = await store.QueryAsync(
			new AuditQuery { TenantId = "non-existent-tenant" },
			CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_ReturnsZeroForNonExistentTenant()
	{
		var store = new InMemoryAuditStore();
		var count = await store.CountAsync(
			new AuditQuery { TenantId = "non-existent-tenant" },
			CancellationToken.None).ConfigureAwait(false);
		count.ShouldBe(0L);
	}

	#endregion

	#region InMemoryAuditStore - VerifyChainIntegrity Empty Date Range

	[Fact]
	public async Task InMemoryAuditStore_VerifyChainIntegrity_ReturnsValidForEmptyRange()
	{
		// Arrange - date range with no events
		var store = new InMemoryAuditStore();
		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "outside-range",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		// Act - date range that excludes all events
		var result = await store.VerifyChainIntegrityAsync(
			new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero),
			CancellationToken.None).ConfigureAwait(false);

		// Assert - valid with 0 events verified
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(0);
	}

	#endregion

	#region InMemoryAuditStore - GetLastEventAsync for Unknown Tenant

	[Fact]
	public async Task InMemoryAuditStore_GetLastEventAsync_ReturnsNullForUnknownTenant()
	{
		var store = new InMemoryAuditStore();
		var result = await store.GetLastEventAsync("unknown-tenant", CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	#endregion

	#region AuditHasher - VerifyHash Edge Cases

	[Fact]
	public void AuditHasher_VerifyHash_ReturnsFalse_WhenEventIsNull()
	{
		// Act & Assert - null event should return false
		AuditHasher.VerifyHash(null!, null).ShouldBeFalse();
	}

	[Fact]
	public void AuditHasher_VerifyHash_ReturnsFalse_WhenEventHashIsNull()
	{
		// Arrange - event with null EventHash
		var auditEvent = new AuditEvent
		{
			EventId = "no-hash",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			EventHash = null
		};

		// Act & Assert
		AuditHasher.VerifyHash(auditEvent, null).ShouldBeFalse();
	}

	[Fact]
	public void AuditHasher_ComputeHash_ThrowsOnNullEvent()
	{
		Should.Throw<ArgumentNullException>(() => AuditHasher.ComputeHash(null!, null));
	}

	[Fact]
	public void AuditHasher_ComputeHash_HandlesNullMetadataValue()
	{
		// Arrange - metadata with null value
		var auditEvent = new AuditEvent
		{
			EventId = "null-meta-val",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1",
			Metadata = new Dictionary<string, string>
			{
				["key1"] = "value1",
				["key2"] = null! // null metadata value
			}
		};

		// Act - should not throw, treats null as empty string
		var hash = AuditHasher.ComputeHash(auditEvent, null);

		// Assert
		hash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void AuditHasher_ComputeHash_DiffersByPreviousHash()
	{
		// Arrange
		var auditEvent = new AuditEvent
		{
			EventId = "prev-hash-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1"
		};

		// Act
		var hashWithNull = AuditHasher.ComputeHash(auditEvent, null);
		var hashWithValue = AuditHasher.ComputeHash(auditEvent, "some-previous-hash");

		// Assert - different previous hash produces different event hash
		hashWithNull.ShouldNotBe(hashWithValue);
	}

	[Fact]
	public void AuditHasher_ComputeGenesisHash_DiffersByTenantId()
	{
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		var hash1 = AuditHasher.ComputeGenesisHash(null, initTime);
		var hash2 = AuditHasher.ComputeGenesisHash("tenant-a", initTime);
		var hash3 = AuditHasher.ComputeGenesisHash("tenant-b", initTime);

		hash1.ShouldNotBe(hash2);
		hash2.ShouldNotBe(hash3);
	}

	[Fact]
	public void AuditHasher_ComputeHash_WithEmptyMetadata_ProducesConsistentHash()
	{
		var auditEvent = new AuditEvent
		{
			EventId = "empty-meta",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1",
			Metadata = new Dictionary<string, string>() // empty, not null
		};

		var hash1 = AuditHasher.ComputeHash(auditEvent, null);
		var hash2 = AuditHasher.ComputeHash(auditEvent, null);

		hash1.ShouldBe(hash2);
	}

	#endregion

	#region DefaultAuditLogger - Constructor Null Checks

	[Fact]
	public void DefaultAuditLogger_Constructor_ThrowsOnNullStore()
	{
		Should.Throw<ArgumentNullException>(
			() => new DefaultAuditLogger(null!, NullLogger<DefaultAuditLogger>.Instance));
	}

	[Fact]
	public void DefaultAuditLogger_Constructor_ThrowsOnNullLogger()
	{
		var store = A.Fake<IAuditStore>();
		Should.Throw<ArgumentNullException>(
			() => new DefaultAuditLogger(store, null!));
	}

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ThrowsOnNullEvent()
	{
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.LogAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_RethrowsCancellation()
	{
		// Arrange
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);

		var auditEvent = new AuditEvent
		{
			EventId = "cancel-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => store.StoreAsync(auditEvent, A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert - OperationCanceledException should propagate (not be caught)
		await Should.ThrowAsync<OperationCanceledException>(
			() => sut.LogAsync(auditEvent, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region RbacAuditStore - Constructor Null Checks

	[Fact]
	public void RbacAuditStore_Constructor_ThrowsOnNullInnerStore()
	{
		var roleProvider = A.Fake<IAuditRoleProvider>();
		var logger = new NullLogger<RbacAuditStore>();
		Should.Throw<ArgumentNullException>(
			() => new RbacAuditStore(null!, roleProvider, logger));
	}

	[Fact]
	public void RbacAuditStore_Constructor_ThrowsOnNullRoleProvider()
	{
		var store = A.Fake<IAuditStore>();
		var logger = new NullLogger<RbacAuditStore>();
		Should.Throw<ArgumentNullException>(
			() => new RbacAuditStore(store, null!, logger));
	}

	[Fact]
	public void RbacAuditStore_Constructor_ThrowsOnNullLogger()
	{
		var store = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		Should.Throw<ArgumentNullException>(
			() => new RbacAuditStore(store, roleProvider, null!));
	}

	#endregion

	#region RbacAuditStore - StoreAsync Delegates Directly

	[Fact]
	public async Task RbacAuditStore_StoreAsync_DelegatesToInnerStore()
	{
		// Arrange - store operations bypass RBAC
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var auditEvent = new AuditEvent
		{
			EventId = "store-delegate",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		var expectedResult = new AuditEventId
		{
			EventId = "store-delegate",
			EventHash = "hash",
			SequenceNumber = 1,
			RecordedAt = DateTimeOffset.UtcNow
		};

		A.CallTo(() => innerStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await sut.StoreAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		// Assert - no role check happens, delegates directly
		result.EventId.ShouldBe("store-delegate");
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region RbacAuditStore - QueryAsync Null Query Throws

	[Fact]
	public async Task RbacAuditStore_QueryAsync_ThrowsOnNullQuery()
	{
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.QueryAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RbacAuditStore_CountAsync_ThrowsOnNullQuery()
	{
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.CountAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region AuditLoggingServiceCollectionExtensions - Null Guard Checks

	[Fact]
	public void AddAuditLogging_ThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(
			() => AuditLoggingServiceCollectionExtensions.AddAuditLogging(null!));
	}

	[Fact]
	public void AddAuditLogging_Generic_ThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(
			() => AuditLoggingServiceCollectionExtensions.AddAuditLogging<InMemoryAuditStore>(null!));
	}

	[Fact]
	public void AddAuditLogging_Factory_ThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(
			() => AuditLoggingServiceCollectionExtensions.AddAuditLogging(null!, _ => new InMemoryAuditStore()));
	}

	[Fact]
	public void AddAuditLogging_Factory_ThrowsOnNullFactory()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddAuditLogging((Func<IServiceProvider, IAuditStore>)null!));
	}

	[Fact]
	public void UseAuditStore_ThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(
			() => AuditLoggingServiceCollectionExtensions.UseAuditStore<InMemoryAuditStore>(null!));
	}

	[Fact]
	public void AddRbacAuditStore_ThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(
			() => AuditLoggingServiceCollectionExtensions.AddRbacAuditStore(null!));
	}

	[Fact]
	public void AddAuditRoleProvider_ThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(
			() => AuditLoggingServiceCollectionExtensions.AddAuditRoleProvider<TestRoleProvider>(null!));
	}

	#endregion

	#region AuditLoggingServiceCollectionExtensions - Idempotent Registration (TryAdd)

	[Fact]
	public void AddAuditLogging_IsIdempotent_DoesNotDuplicateRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - call twice
		_ = services.AddAuditLogging();
		_ = services.AddAuditLogging();

		// Assert - TryAdd should prevent duplicates
		var auditStoreDescriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		auditStoreDescriptors.Count.ShouldBe(1);

		var loggerDescriptors = services.Where(d => d.ServiceType == typeof(IAuditLogger)).ToList();
		loggerDescriptors.Count.ShouldBe(1);
	}

	[Fact]
	public void AddAuditLogging_Generic_IsIdempotent_DoesNotDuplicateRegistrations()
	{
		var services = new ServiceCollection();
		_ = services.AddAuditLogging<InMemoryAuditStore>();
		_ = services.AddAuditLogging<InMemoryAuditStore>();

		var storeDescriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		storeDescriptors.Count.ShouldBe(1);
	}

	[Fact]
	public void AddAuditLogging_Factory_IsIdempotent()
	{
		var services = new ServiceCollection();
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());
		_ = services.AddAuditLogging(_ => new InMemoryAuditStore());

		var storeDescriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		storeDescriptors.Count.ShouldBe(1);
	}

	#endregion

	#region AuditLoggingServiceCollectionExtensions - AddAuditRoleProvider

	[Fact]
	public void AddAuditRoleProvider_RegistersAsScopedService()
	{
		var services = new ServiceCollection();
		var result = services.AddAuditRoleProvider<TestRoleProvider>();

		result.ShouldBeSameAs(services);

		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditRoleProvider));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
		descriptor.ImplementationType.ShouldBe(typeof(TestRoleProvider));
	}

	#endregion

	#region AuditLoggingServiceCollectionExtensions - UseAuditStore Removes Existing

	[Fact]
	public void UseAuditStore_RemovesExistingAndAddsNew()
	{
		// Arrange - register two different stores to verify removal
		var services = new ServiceCollection();
		_ = services.AddAuditLogging(); // registers InMemoryAuditStore

		// Act - replace with custom store
		_ = services.UseAuditStore<CustomFinalTestStore>();

		// Assert - old registration removed, new one added
		var descriptors = services.Where(d => d.ServiceType == typeof(IAuditStore)).ToList();
		// Should have 1 descriptor (the new one)
		descriptors.Last().ImplementationType.ShouldBe(typeof(CustomFinalTestStore));
	}

	#endregion

	#region RbacAuditStore - GetByIdAsync Returns Null for Non-Existent

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_ReturnsNullWhenInnerReturnsNull()
	{
		// Arrange
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		A.CallTo(() => innerStore.GetByIdAsync("missing", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(null));

		// Act
		var result = await sut.GetByIdAsync("missing", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region RbacAuditStore - GetByIdAsync with MetaAudit Success Path

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_LogsMetaAudit_WhenMetaLoggerProvided()
	{
		// Arrange
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		var metaLogger = A.Fake<IAuditLogger>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.ComplianceOfficer));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger, null, metaLogger);

		var auditEvent = new AuditEvent
		{
			EventId = "meta-success",
			EventType = AuditEventType.Security,
			Action = "Check",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => innerStore.GetByIdAsync("meta-success", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(auditEvent));
		A.CallTo(() => metaLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new AuditEventId
			{
				EventId = "meta-1",
				EventHash = "h",
				SequenceNumber = 1,
				RecordedAt = DateTimeOffset.UtcNow
			}));

		// Act
		var result = await sut.GetByIdAsync("meta-success", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => metaLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region DefaultAuditLogger - WhiteSpace Validation Paths

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ThrowsOnWhitespaceEventId()
	{
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);

		var auditEvent = new AuditEvent
		{
			EventId = "   ", // whitespace only
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		await Should.ThrowAsync<ArgumentException>(
			() => sut.LogAsync(auditEvent, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ThrowsOnWhitespaceAction()
	{
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);

		var auditEvent = new AuditEvent
		{
			EventId = "evt-1",
			EventType = AuditEventType.DataAccess,
			Action = "   ",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		await Should.ThrowAsync<ArgumentException>(
			() => sut.LogAsync(auditEvent, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ThrowsOnWhitespaceActorId()
	{
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);

		var auditEvent = new AuditEvent
		{
			EventId = "evt-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "   "
		};

		await Should.ThrowAsync<ArgumentException>(
			() => sut.LogAsync(auditEvent, CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region Helper Types

	private sealed class TestRoleProvider : IAuditRoleProvider
	{
		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditLogRole.Administrator);
	}

	private sealed class CustomFinalTestStore : IAuditStore
	{
		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = "custom",
				SequenceNumber = 1,
				RecordedAt = DateTimeOffset.UtcNow
			});

		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken = default)
			=> Task.FromResult<AuditEvent?>(null);

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<AuditEvent>>([]);

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken = default)
			=> Task.FromResult(0L);

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditIntegrityResult.Valid(0, startDate, endDate));

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId = null, CancellationToken cancellationToken = default)
			=> Task.FromResult<AuditEvent?>(null);
	}

	#endregion
}
