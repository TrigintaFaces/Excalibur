// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.AuditLogging;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Targeted tests to push Excalibur.Dispatch.AuditLogging coverage past 95%.
/// Focuses on:
/// - VerifyChainIntegrityAsync hash mismatch path (lines 193-209)
/// - GetPreviousHash empty tenant list edge case (lines 263-265)
/// - LoggerMessage generated code branches (IsEnabled checks)
/// - InMemoryAuditStore edge cases
/// - AuditLoggingServiceCollectionExtensions uncovered branches
/// - RbacAuditStore.ApplyRoleFilters fallback path
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[UnitTest]
public sealed class AuditLoggingCoveragePushShould
{
	#region InMemoryAuditStore - VerifyChainIntegrityAsync Hash Mismatch (Critical Coverage Gap)

	[Fact]
	public async Task InMemoryAuditStore_VerifyChainIntegrity_DetectsHashMismatch_WhenEventTampered()
	{
		// Arrange - store valid events, then tamper with one via reflection to trigger
		// the hash mismatch path (lines 193-209 in InMemoryAuditStore.cs)
		var store = new InMemoryAuditStore();
		var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "tamper-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-1"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "tamper-2",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp.AddMinutes(1),
			ActorId = "user-1"
		}, CancellationToken.None);

		// Tamper: replace the second event with a modified version that has wrong hash
		var eventsById = GetPrivateField<ConcurrentDictionary<string, AuditEvent>>(store, "_eventsById");
		if (eventsById.TryGetValue("tamper-2", out var originalEvent))
		{
			// Change the EventHash to a wrong value, causing VerifyHash to fail
			var tamperedEvent = originalEvent with { EventHash = "TAMPERED_HASH_VALUE" };
			eventsById["tamper-2"] = tamperedEvent;
		}

		// Act - this should detect the hash mismatch
		var result = await store.VerifyChainIntegrityAsync(
			timestamp.AddHours(-1),
			timestamp.AddHours(1),
			CancellationToken.None);

		// Assert - should report integrity violation
		result.IsValid.ShouldBeFalse();
		result.ViolationCount.ShouldBeGreaterThan(0);
		result.FirstViolationEventId.ShouldNotBeNull();
		result.ViolationDescription.ShouldNotBeNull();
		result.ViolationDescription.ShouldContain("Hash mismatch");
	}

	[Fact]
	public async Task InMemoryAuditStore_VerifyChainIntegrity_DetectsMultipleViolations_WhenMultipleEventsTampered()
	{
		// Arrange - tamper with multiple events to exercise firstViolationEventId ??= branch
		var store = new InMemoryAuditStore();
		var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

		for (var i = 1; i <= 3; i++)
		{
			_ = await store.StoreAsync(new AuditEvent
			{
				EventId = $"multi-tamper-{i}",
				EventType = AuditEventType.DataAccess,
				Action = "Read",
				Outcome = AuditOutcome.Success,
				Timestamp = timestamp.AddMinutes(i),
				ActorId = "user-1"
			}, CancellationToken.None);
		}

		// Tamper with events 2 and 3
		var eventsById = GetPrivateField<ConcurrentDictionary<string, AuditEvent>>(store, "_eventsById");
		foreach (var key in new[] { "multi-tamper-2", "multi-tamper-3" })
		{
			if (eventsById.TryGetValue(key, out var evt))
			{
				eventsById[key] = evt with { EventHash = "WRONG_HASH" };
			}
		}

		// Act
		var result = await store.VerifyChainIntegrityAsync(
			timestamp.AddHours(-1),
			timestamp.AddHours(1),
			CancellationToken.None);

		// Assert - should report 2 violations, first one captured
		result.IsValid.ShouldBeFalse();
		result.ViolationCount.ShouldBe(2);
		result.FirstViolationEventId.ShouldBe("multi-tamper-2");
	}

	[Fact]
	public async Task InMemoryAuditStore_VerifyChainIntegrity_DetectsFirstEventTampered()
	{
		// Arrange - tamper with the first event specifically
		var store = new InMemoryAuditStore();
		var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "first-tamper-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-1"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "first-tamper-2",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp.AddMinutes(1),
			ActorId = "user-1"
		}, CancellationToken.None);

		// Tamper with the FIRST event
		var eventsById = GetPrivateField<ConcurrentDictionary<string, AuditEvent>>(store, "_eventsById");
		if (eventsById.TryGetValue("first-tamper-1", out var evt))
		{
			eventsById["first-tamper-1"] = evt with { EventHash = "BAD_HASH" };
		}

		// Act
		var result = await store.VerifyChainIntegrityAsync(
			timestamp.AddHours(-1),
			timestamp.AddHours(1),
			CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FirstViolationEventId.ShouldBe("first-tamper-1");
		result.ViolationDescription.ShouldContain("first-tamper-1");
	}

	#endregion

	#region InMemoryAuditStore - GetPreviousHash Empty Tenant List (lines 263-265)

	[Fact]
	public async Task InMemoryAuditStore_StoreAsync_AfterClearingTenantEvents_UsesGenesisHash()
	{
		// Arrange - store an event to create a tenant entry, then clear the tenant list
		// to trigger the "lastEvent is null" branch in GetPreviousHash (line 263-265)
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "genesis-edge-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			TenantId = "genesis-tenant"
		}, CancellationToken.None);

		// Use reflection to clear the tenant's event list but keep the key
		var eventsByTenant = GetPrivateField<ConcurrentDictionary<string, List<AuditEvent>>>(store, "_eventsByTenant");
		if (eventsByTenant.TryGetValue("genesis-tenant", out var tenantEvents))
		{
			lock (tenantEvents)
			{
				tenantEvents.Clear();
			}
		}

		// Act - store another event. GetPreviousHash should find the empty list
		// and take the "lastEvent is null" branch, returning genesis hash
		var result = await store.StoreAsync(new AuditEvent
		{
			EventId = "genesis-edge-2",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			TenantId = "genesis-tenant"
		}, CancellationToken.None);

		// Assert - should successfully store with genesis hash as previous
		result.EventId.ShouldBe("genesis-edge-2");
		result.EventHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task InMemoryAuditStore_StoreAsync_AfterClearingDefaultTenantEvents_UsesGenesisHash()
	{
		// Arrange - same as above but for default tenant (null TenantId)
		// This exercises the ternary "tenantKey == '_default_' ? null : tenantKey"
		// inside the empty-list genesis hash branch
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "default-genesis-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
			// No TenantId = default tenant
		}, CancellationToken.None);

		// Clear the default tenant's event list
		var eventsByTenant = GetPrivateField<ConcurrentDictionary<string, List<AuditEvent>>>(store, "_eventsByTenant");
		if (eventsByTenant.TryGetValue("_default_", out var tenantEvents))
		{
			lock (tenantEvents)
			{
				tenantEvents.Clear();
			}
		}

		// Act - store another event for default tenant
		var result = await store.StoreAsync(new AuditEvent
		{
			EventId = "default-genesis-2",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("default-genesis-2");
		result.EventHash.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region DefaultAuditLogger with Real Logger (LoggerMessage generated code coverage)

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ExercisesLoggerMessageGenCode_WhenLoggingEnabled()
	{
		// Arrange - use a real logger factory with Debug level enabled to trigger
		// the IsEnabled(LogLevel.Debug) branch in the generated LogAuditEventStored
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Debug));
		var logger = loggerFactory.CreateLogger<DefaultAuditLogger>();

		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, logger);

		var auditEvent = CreateValidAuditEvent("log-gen-1");
		var expectedResult = new AuditEventId
		{
			EventId = "log-gen-1",
			EventHash = "hash-1",
			SequenceNumber = 1,
			RecordedAt = DateTimeOffset.UtcNow
		};
		A.CallTo(() => store.StoreAsync(auditEvent, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act - this exercises the LogAuditEventStored generated code with IsEnabled=true
		var result = await sut.LogAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("log-gen-1");
		result.EventHash.ShouldBe("hash-1");
	}

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ExercisesErrorLogPath_WhenLoggingEnabled()
	{
		// Arrange - use a real logger with Error level enabled to trigger
		// the IsEnabled(LogLevel.Error) branch in LogAuditEventStoreFailed
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Error));
		var logger = loggerFactory.CreateLogger<DefaultAuditLogger>();

		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, logger);

		var auditEvent = CreateValidAuditEvent("log-err-1");
		A.CallTo(() => store.StoreAsync(auditEvent, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Simulated store failure"));

		// Act - triggers LogAuditEventStoreFailed with logging enabled
		var result = await sut.LogAsync(auditEvent, CancellationToken.None);

		// Assert - returns failure indicator, doesn't throw
		result.EventId.ShouldBe("log-err-1");
		result.EventHash.ShouldBeEmpty();
		result.SequenceNumber.ShouldBe(-1);
	}

	[Fact]
	public async Task DefaultAuditLogger_VerifyIntegrityAsync_ExercisesInfoLogPaths_WhenLoggingEnabled()
	{
		// Arrange - use real logger with Information level to trigger
		// LogIntegrityVerificationStarted and LogIntegrityVerificationCompleted
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Information));
		var logger = loggerFactory.CreateLogger<DefaultAuditLogger>();

		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, logger);

		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero);
		var validResult = AuditIntegrityResult.Valid(42, startDate, endDate);

		A.CallTo(() => store.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.Returns(Task.FromResult(validResult));

		// Act - exercises LogIntegrityVerificationStarted + LogIntegrityVerificationCompleted
		var result = await sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(42);
	}

	[Fact]
	public async Task DefaultAuditLogger_VerifyIntegrityAsync_ExercisesWarningLogPath_WhenIntegrityFails()
	{
		// Arrange - use real logger to trigger LogIntegrityVerificationFailed
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Warning));
		var logger = loggerFactory.CreateLogger<DefaultAuditLogger>();

		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, logger);

		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero);
		var invalidResult = AuditIntegrityResult.Invalid(
			100, startDate, endDate, "evt-50", "Hash mismatch at event 50", 3);

		A.CallTo(() => store.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.Returns(Task.FromResult(invalidResult));

		// Act - exercises LogIntegrityVerificationFailed with logging enabled
		var result = await sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.ViolationCount.ShouldBe(3);
		result.FirstViolationEventId.ShouldBe("evt-50");
	}

	[Fact]
	public async Task DefaultAuditLogger_VerifyIntegrityAsync_ExercisesErrorLogPath_WhenExceptionOccurs()
	{
		// Arrange - use real logger to trigger LogIntegrityVerificationError
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Error));
		var logger = loggerFactory.CreateLogger<DefaultAuditLogger>();

		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, logger);

		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero);

		A.CallTo(() => store.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("DB connection lost"));

		// Act & Assert - exercises LogIntegrityVerificationError, then rethrows
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None));
	}

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_NullLogger_SkipsLogMessage()
	{
		// Arrange - NullLogger has IsEnabled returning false, exercising the !IsEnabled branch
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);

		var auditEvent = CreateValidAuditEvent("null-logger-test");
		A.CallTo(() => store.StoreAsync(auditEvent, A<CancellationToken>._))
			.Returns(Task.FromResult(new AuditEventId
			{
				EventId = "null-logger-test",
				EventHash = "h1",
				SequenceNumber = 1,
				RecordedAt = DateTimeOffset.UtcNow
			}));

		// Act
		var result = await sut.LogAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe("null-logger-test");
	}

	#endregion DefaultAuditLogger with Real Logger

	#region RbacAuditStore with Real Logger (LoggerMessage generated code coverage)

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_ExercisesWarningLog_WhenAccessDeniedToEvent()
	{
		// Arrange - use real logger with Warning enabled to trigger LogAuditLogAccessDenied
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Warning));
		var logger = loggerFactory.CreateLogger<RbacAuditStore>();

		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		// A non-security event that SecurityAnalyst cannot access
		var dataEvent = new AuditEvent
		{
			EventId = "data-evt-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => innerStore.GetByIdAsync("data-evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(dataEvent));

		// Act - exercises LogAuditLogAccessDenied with logging enabled
		var result = await sut.GetByIdAsync("data-evt-1", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RbacAuditStore_VerifyChainIntegrityAsync_ExercisesWarningLog_WhenAccessDenied()
	{
		// Arrange - use real logger with Warning enabled to trigger LogIntegrityVerificationAccessDenied
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Warning));
		var logger = loggerFactory.CreateLogger<RbacAuditStore>();

		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		// Act & Assert - exercises LogIntegrityVerificationAccessDenied with logging enabled
		await Should.ThrowAsync<UnauthorizedAccessException>(
			() => sut.VerifyChainIntegrityAsync(
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow,
				CancellationToken.None));
	}

	[Fact]
	public async Task RbacAuditStore_MetaAuditFailure_ExercisesWarningLog_WhenLoggingEnabled()
	{
		// Arrange - use real logger to trigger LogMetaAuditLogFailed
		using var loggerFactory = LoggerFactory.Create(builder =>
			builder.SetMinimumLevel(LogLevel.Warning));
		var logger = loggerFactory.CreateLogger<RbacAuditStore>();

		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		var metaLogger = A.Fake<IAuditLogger>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.Administrator));

		var sut = new RbacAuditStore(innerStore, roleProvider, logger, null, metaLogger);

		var auditEvent = new AuditEvent
		{
			EventId = "meta-fail-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => innerStore.GetByIdAsync("meta-fail-test", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(auditEvent));
		A.CallTo(() => metaLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Meta-audit DB down"));

		// Act - exercises LogMetaAuditLogFailed with logging enabled
		var result = await sut.GetByIdAsync("meta-fail-test", CancellationToken.None);

		// Assert - main operation should succeed despite meta-audit failure
		result.ShouldNotBeNull();
		result.EventId.ShouldBe("meta-fail-test");
	}

	#endregion RbacAuditStore with Real Logger

	#region InMemoryAuditStore - Additional Branch Coverage

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_WithAllFiltersOnNoTenant()
	{
		// Arrange - exercise the no-tenant branch of CountAsync with all filters
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-1",
			EventType = AuditEventType.Authentication,
			Action = "Login",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1",
			ResourceId = "res-1",
			ResourceType = "Session",
			CorrelationId = "corr-1",
			IpAddress = "10.0.0.1",
			ResourceClassification = DataClassification.Internal
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Failure,
			Timestamp = new DateTimeOffset(2025, 6, 16, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-2"
		}, CancellationToken.None);

		// Act - count with all filters, no tenant specified (uses _eventsById.Values path)
		var count = await store.CountAsync(new AuditQuery
		{
			ActorId = "user-1",
			EventTypes = [AuditEventType.Authentication],
			Outcomes = [AuditOutcome.Success],
			ResourceId = "res-1",
			ResourceType = "Session",
			CorrelationId = "corr-1",
			IpAddress = "10.0.0.1",
			MinimumClassification = DataClassification.Internal,
			Action = "Login",
			StartDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
			EndDate = new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero)
		}, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_OrderByDescendingFalse_WithTenantScope()
	{
		// Arrange - exercise ascending order within tenant scope
		var store = new InMemoryAuditStore();
		var ts1 = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
		var ts2 = new DateTimeOffset(2025, 6, 20, 0, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "asc-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts2,
			ActorId = "user-1",
			TenantId = "asc-tenant"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "asc-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts1,
			ActorId = "user-1",
			TenantId = "asc-tenant"
		}, CancellationToken.None);

		// Act - ascending order within tenant scope
		var results = await store.QueryAsync(new AuditQuery
		{
			TenantId = "asc-tenant",
			OrderByDescending = false
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results[0].EventId.ShouldBe("asc-2"); // older first
		results[1].EventId.ShouldBe("asc-1"); // newer second
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_WithActionFilter_NoTenant()
	{
		// Arrange - exercise Action filter in CountAsync without tenant
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "act-cnt-1",
			EventType = AuditEventType.DataAccess,
			Action = "Create",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "act-cnt-2",
			EventType = AuditEventType.DataAccess,
			Action = "Delete",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var count = await store.CountAsync(new AuditQuery { Action = "Create" }, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task InMemoryAuditStore_GetLastEventAsync_ReturnsLastForDefaultTenant()
	{
		// Arrange - exercise GetLastEventAsync with explicit null tenant
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "last-def-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "last-def-2",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var lastEvent = await store.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		lastEvent.ShouldNotBeNull();
		lastEvent.EventId.ShouldBe("last-def-2");
	}

	[Fact]
	public async Task InMemoryAuditStore_VerifyChainIntegrity_MultipleEventsSameTimestamp()
	{
		// Arrange - events with same timestamp, ordered by EventId
		var store = new InMemoryAuditStore();
		var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "same-ts-b",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-1"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "same-ts-a",
			EventType = AuditEventType.DataAccess,
			Action = "Write",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act - verify chain integrity handles same-timestamp events via EventId ordering
		var result = await store.VerifyChainIntegrityAsync(
			timestamp.AddHours(-1), timestamp.AddHours(1), CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(2);
	}

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_PaginationWithTenantScope()
	{
		// Arrange - exercise pagination within tenant-scoped query
		var store = new InMemoryAuditStore();

		for (var i = 1; i <= 5; i++)
		{
			_ = await store.StoreAsync(new AuditEvent
			{
				EventId = $"pag-{i}",
				EventType = AuditEventType.DataAccess,
				Action = "Read",
				Outcome = AuditOutcome.Success,
				Timestamp = DateTimeOffset.UtcNow.AddMinutes(i),
				ActorId = "user-1",
				TenantId = "pag-tenant"
			}, CancellationToken.None);
		}

		// Act
		var page1 = await store.QueryAsync(new AuditQuery
		{
			TenantId = "pag-tenant",
			MaxResults = 2,
			Skip = 0,
			OrderByDescending = false
		}, CancellationToken.None);

		var page2 = await store.QueryAsync(new AuditQuery
		{
			TenantId = "pag-tenant",
			MaxResults = 2,
			Skip = 2,
			OrderByDescending = false
		}, CancellationToken.None);

		// Assert
		page1.Count.ShouldBe(2);
		page2.Count.ShouldBe(2);
		page1[0].EventId.ShouldNotBe(page2[0].EventId);
	}

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_DescendingOrder_WithTenantScope()
	{
		// Arrange - exercise descending order within tenant scope
		var store = new InMemoryAuditStore();
		var ts1 = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
		var ts2 = new DateTimeOffset(2025, 6, 20, 0, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "desc-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts1,
			ActorId = "user-1",
			TenantId = "desc-tenant"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "desc-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts2,
			ActorId = "user-1",
			TenantId = "desc-tenant"
		}, CancellationToken.None);

		// Act - descending order within tenant scope
		var results = await store.QueryAsync(new AuditQuery
		{
			TenantId = "desc-tenant",
			OrderByDescending = true
		}, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results[0].EventId.ShouldBe("desc-2"); // newer first
		results[1].EventId.ShouldBe("desc-1"); // older second
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_WithOutcomes_NoTenant()
	{
		// Arrange - exercise Outcomes filter branch in CountAsync without tenant
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "out-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "out-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Denied,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		}, CancellationToken.None);

		// Act
		var count = await store.CountAsync(new AuditQuery
		{
			Outcomes = [AuditOutcome.Denied]
		}, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_WithResourceType_NoTenant()
	{
		// Arrange
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "rt-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			ResourceType = "Customer"
		}, CancellationToken.None);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "rt-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			ResourceType = "Order"
		}, CancellationToken.None);

		// Act
		var count = await store.CountAsync(new AuditQuery { ResourceType = "Customer" }, CancellationToken.None);

		// Assert
		count.ShouldBe(1);
	}

	#endregion InMemoryAuditStore - Additional Branch Coverage

	#region AuditLoggingServiceCollectionExtensions - Uncovered Branches

	[Fact]
	public void AddRbacAuditStore_WithScopedLifetime_ImplementationType_PreservesScope()
	{
		// Arrange - test with a scoped lifetime to exercise lifetime preservation
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Manually add a scoped IAuditStore to exercise the scoped lifetime path
		_ = services.AddScoped<IAuditStore, CustomTestAuditStore>();
		_ = services.AddScoped<IAuditRoleProvider, AlwaysAdminRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - should preserve scoped lifetime
		var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
		descriptor.ImplementationFactory.ShouldNotBeNull();

		// Original type re-registered
		var concreteDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CustomTestAuditStore));
		concreteDescriptor.ShouldNotBeNull();
		concreteDescriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddRbacAuditStore_WithTransientLifetime_ImplementationType_PreservesTransient()
	{
		// Arrange - test with a transient lifetime
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddTransient<IAuditStore, CustomTestAuditStore>();
		_ = services.AddScoped<IAuditRoleProvider, AlwaysAdminRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert
		var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void AddRbacAuditStore_FactoryBranch_WithScopedLifetime_PreservesScope()
	{
		// Arrange - factory with scoped lifetime using explicit ServiceDescriptor
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register with factory AND scoped lifetime via ServiceDescriptor
		var scopedFactoryDescriptor = new ServiceDescriptor(
			typeof(IAuditStore),
			_ => new InMemoryAuditStore(),
			ServiceLifetime.Scoped);
		((ICollection<ServiceDescriptor>)services).Add(scopedFactoryDescriptor);

		_ = services.AddScoped<IAuditRoleProvider, AlwaysAdminRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert
		var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
		descriptor.ImplementationFactory.ShouldNotBeNull();
	}

	[Fact]
	public void AddAuditLogging_DefaultOverload_ThenAddRbacAuditStore_DecoratorResolvesCorrectly()
	{
		// Arrange - AddAuditLogging() registers IAuditStore with factory (sp => sp.GetRequiredService<InMemoryAuditStore>())
		// After AddRbacAuditStore, the decorator should wrap the factory-based registration
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging();
		_ = services.AddScoped<IAuditRoleProvider, AlwaysAdminRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - IAuditStore should be factory-based (decorator)
		var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
		descriptor.ImplementationFactory.ShouldNotBeNull();
	}

	[Fact]
	public void UseAuditStore_AfterAddRbacAuditStore_ReplacesDecorator()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuditLogging<CustomTestAuditStore>();
		_ = services.AddScoped<IAuditRoleProvider, AlwaysAdminRoleProvider>();
		_ = services.AddRbacAuditStore();

		// Act - UseAuditStore should replace the decorator with a direct registration
		_ = services.UseAuditStore<InMemoryAuditStore>();

		// Assert
		var lastDescriptor = services.Last(d => d.ServiceType == typeof(IAuditStore));
		lastDescriptor.ImplementationType.ShouldBe(typeof(InMemoryAuditStore));
	}

	#endregion AuditLoggingServiceCollectionExtensions - Uncovered Branches

	#region RbacAuditStore - ApplyRoleFilters Fallback and CanAccessEvent Edge Cases

	[Fact]
	public async Task RbacAuditStore_QueryAsync_SecurityAnalyst_WithNoRequestedTypes_UsesAllSecurityTypes()
	{
		// Arrange - SecurityAnalyst with null/empty EventTypes should get all 3 security types
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		AuditQuery? capturedQuery = null;
		A.CallTo(() => innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

		// Act - query with null EventTypes
		await sut.QueryAsync(new AuditQuery { EventTypes = null }, CancellationToken.None);

		// Assert - should be restricted to security types
		capturedQuery.ShouldNotBeNull();
		capturedQuery.EventTypes.ShouldNotBeNull();
		capturedQuery.EventTypes.Count.ShouldBe(3);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Authentication);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Authorization);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Security);
	}

	[Fact]
	public async Task RbacAuditStore_CountAsync_SecurityAnalyst_WithNoRequestedTypes_UsesAllSecurityTypes()
	{
		// Arrange
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		AuditQuery? capturedQuery = null;
		A.CallTo(() => innerStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Task.FromResult(3L));

		// Act
		var count = await sut.CountAsync(new AuditQuery(), CancellationToken.None);

		// Assert
		count.ShouldBe(3L);
		capturedQuery.ShouldNotBeNull();
		capturedQuery.EventTypes.ShouldNotBeNull();
		capturedQuery.EventTypes.Count.ShouldBe(3);
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_SecurityAnalyst_CanAccessAuthorizationEvent()
	{
		// Arrange - verify SecurityAnalyst can access Authorization event type
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var authzEvent = new AuditEvent
		{
			EventId = "authz-1",
			EventType = AuditEventType.Authorization,
			Action = "CheckPermission",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => innerStore.GetByIdAsync("authz-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(authzEvent));

		// Act
		var result = await sut.GetByIdAsync("authz-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.EventType.ShouldBe(AuditEventType.Authorization);
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_SecurityAnalyst_DeniesDataModificationEvent()
	{
		// Arrange - verify SecurityAnalyst cannot access DataModification event type
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
		var logger = loggerFactory.CreateLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var dataModEvent = new AuditEvent
		{
			EventId = "mod-1",
			EventType = AuditEventType.DataModification,
			Action = "Update",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => innerStore.GetByIdAsync("mod-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(dataModEvent));

		// Act
		var result = await sut.GetByIdAsync("mod-1", CancellationToken.None);

		// Assert - filtered out
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RbacAuditStore_QueryAsync_SecurityAnalyst_IntersectsMultipleSecurityTypes()
	{
		// Arrange - request Authentication + Authorization + DataAccess
		// Should return only Authentication + Authorization (both in SecurityEventTypes)
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		AuditQuery? capturedQuery = null;
		A.CallTo(() => innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

		// Act
		await sut.QueryAsync(new AuditQuery
		{
			EventTypes = [AuditEventType.Authentication, AuditEventType.Authorization, AuditEventType.DataAccess]
		}, CancellationToken.None);

		// Assert - DataAccess should be filtered out, keeping only Auth + Authz
		capturedQuery.ShouldNotBeNull();
		capturedQuery.EventTypes.ShouldNotBeNull();
		capturedQuery.EventTypes.Count.ShouldBe(2);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Authentication);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Authorization);
		capturedQuery.EventTypes.ShouldNotContain(AuditEventType.DataAccess);
	}

	[Fact]
	public async Task RbacAuditStore_QueryAsync_ComplianceOfficer_PassesQueryUnmodified()
	{
		// Arrange - verify ComplianceOfficer query passes through ApplyRoleFilters unchanged
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.ComplianceOfficer));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var originalQuery = new AuditQuery
		{
			EventTypes = [AuditEventType.DataAccess],
			ActorId = "specific-actor",
			TenantId = "my-tenant"
		};

		AuditQuery? capturedQuery = null;
		A.CallTo(() => innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Task.FromResult<IReadOnlyList<AuditEvent>>([]));

		// Act
		await sut.QueryAsync(originalQuery, CancellationToken.None);

		// Assert - query should be the same reference (not modified)
		capturedQuery.ShouldBeSameAs(originalQuery);
	}

	#endregion RbacAuditStore - ApplyRoleFilters Fallback

	#region Helper Types and Methods

	private static T GetPrivateField<T>(object obj, string fieldName)
	{
		var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException($"Field '{fieldName}' not found on {obj.GetType().Name}");
		return (T)(field.GetValue(obj) ?? throw new InvalidOperationException($"Field '{fieldName}' is null"));
	}

	private sealed class CustomTestAuditStore : IAuditStore
	{
		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = "custom-hash",
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

	private sealed class AlwaysAdminRoleProvider : IAuditRoleProvider
	{
		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditLogRole.Administrator);
	}

	private static AuditEvent CreateValidAuditEvent(string eventId) =>
		new()
		{
			EventId = eventId,
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero),
			ActorId = "user-123",
			ActorType = "User"
		};

	#endregion Helper Types and Methods
}
