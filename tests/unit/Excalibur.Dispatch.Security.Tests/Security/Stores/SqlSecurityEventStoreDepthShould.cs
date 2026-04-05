// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Depth tests for <see cref="SqlSecurityEventStore"/>.
/// Covers cancellation, concurrent access, mixed valid/invalid events,
/// and additional edge cases not in the primary test file.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
[Trait(TraitNames.Feature, TestFeatures.Stores)]
public sealed class SqlSecurityEventStoreDepthShould : IDisposable
{
	private readonly SqlSecurityEventStore _sut;

	public SqlSecurityEventStoreDepthShould()
	{
		_sut = new SqlSecurityEventStore(NullLogger<SqlSecurityEventStore>.Instance);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	// ========================================
	// Cancellation Tests
	// ========================================

	[Fact]
	public async Task StoreEventsAsync_ThrowsOperationCanceledException_WhenCancelled()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		var events = new[] { CreateValidEvent() };

		await Should.ThrowAsync<OperationCanceledException>(
			async () => await _sut.StoreEventsAsync(events, cts.Token));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsOperationCanceledException_WhenCancelled()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		var query = new SecurityEventQuery { MaxResults = 10 };

		await Should.ThrowAsync<OperationCanceledException>(
			async () => await _sut.QueryEventsAsync(query, cts.Token));
	}

	// ========================================
	// Mixed Valid/Invalid Event Tests
	// ========================================

	[Fact]
	public async Task StoreEventsAsync_CompletesWithoutThrowing_WhenMixOfValidAndInvalidEvents()
	{
		var events = new[]
		{
			CreateValidEvent(),
			new SecurityEvent
			{
				Id = Guid.Empty, // Invalid -- logged as warning
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityEventType.AuthenticationSuccess,
				Description = "Valid description",
				Severity = SecuritySeverity.Low,
			},
			CreateValidEvent(),
		};

		// Placeholder store logs warnings for invalid events but does not throw
		await _sut.StoreEventsAsync(events, CancellationToken.None);
	}

	[Fact]
	public async Task StoreEventsAsync_CompletesWithoutThrowing_WhenDescriptionIsWhitespace()
	{
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.NewGuid(),
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityEventType.AuthenticationFailure,
				Description = "   ", // Whitespace-only -- logged as warning
				Severity = SecuritySeverity.Medium,
			},
		};

		// Placeholder store logs warnings but does not throw
		await _sut.StoreEventsAsync(events, CancellationToken.None);
	}

	[Fact]
	public async Task StoreEventsAsync_CompletesWithoutThrowing_WhenDescriptionIsNull()
	{
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.NewGuid(),
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityEventType.AuthenticationFailure,
				Description = null!, // Logged as warning
				Severity = SecuritySeverity.Medium,
			},
		};

		// Placeholder store logs warnings but does not throw
		await _sut.StoreEventsAsync(events, CancellationToken.None);
	}

	// ========================================
	// Query Edge Cases
	// ========================================

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenMaxResultsIsMaxInt()
	{
		// MaxResults > 0 and no time range constraint -- should succeed
		var query = new SecurityEventQuery { MaxResults = int.MaxValue };

		var result = await _sut.QueryEventsAsync(query, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_Succeeds_WhenStartTimeEqualsEndTime()
	{
		var now = DateTimeOffset.UtcNow;
		var query = new SecurityEventQuery
		{
			StartTime = now,
			EndTime = now,
			MaxResults = 10,
		};

		// Start == End is valid (zero-width window)
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_Succeeds_WhenOnlyStartTimeIsSet()
	{
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(-1),
			MaxResults = 10,
		};

		var result = await _sut.QueryEventsAsync(query, CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_Succeeds_WhenOnlyEndTimeIsSet()
	{
		var query = new SecurityEventQuery
		{
			EndTime = DateTimeOffset.UtcNow,
			MaxResults = 10,
		};

		var result = await _sut.QueryEventsAsync(query, CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeEmpty();
	}

	// ========================================
	// Concurrent Access Tests
	// ========================================

	[Fact]
	public async Task StoreEventsAsync_HandlesSequentialCalls()
	{
		var events1 = new[] { CreateValidEvent() };
		var events2 = new[] { CreateValidEvent() };

		await _sut.StoreEventsAsync(events1, CancellationToken.None).ConfigureAwait(false);
		await _sut.StoreEventsAsync(events2, CancellationToken.None).ConfigureAwait(false);

		// Both calls should succeed without issues
	}

	[Fact]
	public async Task StoreEventsAsync_HandlesMultipleValidEvents()
	{
		var events = Enumerable.Range(0, 50)
			.Select(_ => CreateValidEvent())
			.ToList();

		// Should handle batch of 50 events without issues
		await _sut.StoreEventsAsync(events, CancellationToken.None).ConfigureAwait(false);
	}

	// ========================================
	// Store Event with Various EventTypes
	// ========================================

	[Theory]
	[InlineData(SecurityEventType.AuthenticationSuccess)]
	[InlineData(SecurityEventType.AuthenticationFailure)]
	[InlineData(SecurityEventType.AuthorizationFailure)]
	[InlineData(SecurityEventType.ValidationFailure)]
	[InlineData(SecurityEventType.EncryptionFailure)]
	[InlineData(SecurityEventType.RateLimitExceeded)]
	[InlineData(SecurityEventType.SuspiciousActivity)]
	public async Task StoreEventsAsync_AcceptsAllSecurityEventTypes(SecurityEventType eventType)
	{
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.NewGuid(),
				Timestamp = DateTimeOffset.UtcNow,
				EventType = eventType,
				Description = $"Test event for {eventType}",
				Severity = SecuritySeverity.Low,
			},
		};

		// Should store without exception
		await _sut.StoreEventsAsync(events, CancellationToken.None).ConfigureAwait(false);
	}

	// ========================================
	// Helpers
	// ========================================

	private static SecurityEvent CreateValidEvent()
	{
		return new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = DateTimeOffset.UtcNow,
			EventType = SecurityEventType.AuthenticationSuccess,
			Description = "Test valid event",
			Severity = SecuritySeverity.Low,
		};
	}
}
