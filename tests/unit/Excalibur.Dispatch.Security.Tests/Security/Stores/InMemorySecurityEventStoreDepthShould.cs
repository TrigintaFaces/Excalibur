// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Deep coverage tests for <see cref="InMemorySecurityEventStore"/> covering
/// combined query filters, boundary conditions, MaxResults edge cases,
/// and multi-batch accumulation behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InMemorySecurityEventStoreDepthShould
{
	private readonly InMemorySecurityEventStore _sut = new();

	[Fact]
	public async Task QueryWithCombinedStartTimeAndEndTime()
	{
		// Arrange — events spread across time
		var now = DateTimeOffset.UtcNow;
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-3)),
			CreateEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-2)),
			CreateEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-1)),
			CreateEvent(SecurityEventType.AuthenticationSuccess, now),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — query window: -2.5h to -0.5h (should match events at -2h and -1h)
		var query = new SecurityEventQuery
		{
			StartTime = now.AddMinutes(-150),
			EndTime = now.AddMinutes(-30),
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(2);
	}

	[Fact]
	public async Task QueryWithCombinedStartTimeEndTimeAndEventType()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-2)),
			CreateEvent(SecurityEventType.AuthenticationFailure, now.AddHours(-1)),
			CreateEvent(SecurityEventType.AuthenticationSuccess, now),
			CreateEvent(SecurityEventType.AuthorizationSuccess, now.AddMinutes(-30)),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — all three filters combined
		var query = new SecurityEventQuery
		{
			StartTime = now.AddMinutes(-150),
			EndTime = now.AddMinutes(-10),
			EventType = SecurityEventType.AuthenticationFailure,
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert — only the AuthenticationFailure at -1h matches all three filters
		result.Count().ShouldBe(1);
		result.First().EventType.ShouldBe(SecurityEventType.AuthenticationFailure);
	}

	[Fact]
	public async Task QueryWithMaxResultsOne_ReturnsSingleEvent()
	{
		// Arrange
		var events = Enumerable.Range(0, 5)
			.Select(_ => CreateEvent(SecurityEventType.ValidationFailure))
			.ToArray();
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 1 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task QueryWithMaxResultsZero_ReturnsEmpty()
	{
		// Arrange
		var events = new[] { CreateEvent(SecurityEventType.AuthenticationSuccess) };
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 0 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryWithNoMatchingEventType_ReturnsEmpty()
	{
		// Arrange — store auth events, query for validation
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess),
			CreateEvent(SecurityEventType.AuthorizationSuccess),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			EventType = SecurityEventType.ValidationFailure,
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryWithFutureStartTime_ReturnsEmpty()
	{
		// Arrange
		var events = new[] { CreateEvent(SecurityEventType.AuthenticationSuccess) };
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(1),
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryWithPastEndTime_ReturnsEmpty()
	{
		// Arrange
		var events = new[] { CreateEvent(SecurityEventType.AuthenticationSuccess) };
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			EndTime = DateTimeOffset.UtcNow.AddHours(-10),
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryExactBoundaryTimestamps()
	{
		// Arrange — event at exact boundary time
		var timestamp = DateTimeOffset.UtcNow;
		var events = new[] { CreateEvent(SecurityEventType.AuthenticationSuccess, timestamp) };
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — query with exact start and end at same timestamp
		var query = new SecurityEventQuery
		{
			StartTime = timestamp,
			EndTime = timestamp,
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert — inclusive boundaries: >= start and <= end
		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task AccumulateLargeNumberOfEvents()
	{
		// Arrange
		for (var batch = 0; batch < 10; batch++)
		{
			var events = Enumerable.Range(0, 10)
				.Select(_ => CreateEvent(SecurityEventType.AuthenticationSuccess))
				.ToArray();
			await _sut.StoreEventsAsync(events, CancellationToken.None);
		}

		// Act
		var query = new SecurityEventQuery { MaxResults = 1000 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(100);
	}

	[Fact]
	public async Task PreserveEventProperties()
	{
		// Arrange
		var id = Guid.NewGuid();
		var timestamp = DateTimeOffset.UtcNow;
		var events = new[]
		{
			new SecurityEvent
			{
				Id = id,
				Timestamp = timestamp,
				EventType = SecurityEventType.AuthenticationFailure,
				Description = "Login failed for user admin",
				Severity = SecuritySeverity.High,
			},
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 1 };
		var result = (await _sut.QueryEventsAsync(query, CancellationToken.None)).First();

		// Assert — all properties preserved
		result.Id.ShouldBe(id);
		result.Timestamp.ShouldBe(timestamp);
		result.EventType.ShouldBe(SecurityEventType.AuthenticationFailure);
		result.Description.ShouldBe("Login failed for user admin");
		result.Severity.ShouldBe(SecuritySeverity.High);
	}

	private static SecurityEvent CreateEvent(SecurityEventType type, DateTimeOffset? timestamp = null)
	{
		return new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = timestamp ?? DateTimeOffset.UtcNow,
			EventType = type,
			Description = $"Test {type}",
			Severity = SecuritySeverity.Low,
		};
	}
}
