// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Unit tests for <see cref="InMemorySecurityEventStore"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Stores")]
public sealed class InMemorySecurityEventStoreShould
{
	private readonly InMemorySecurityEventStore _sut;

	public InMemorySecurityEventStoreShould()
	{
		_sut = new InMemorySecurityEventStore();
	}

	[Fact]
	public void ImplementISecurityEventStore()
	{
		// Assert
		_sut.ShouldBeAssignableTo<ISecurityEventStore>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(InMemorySecurityEventStore).IsNotPublic.ShouldBeTrue();
		typeof(InMemorySecurityEventStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public async Task StoreEventsSuccessfully()
	{
		// Arrange
		var events = new[]
		{
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess),
			CreateSecurityEvent(SecurityEventType.AuthenticationFailure),
		};

		// Act
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Assert - query to verify stored
		var query = new SecurityEventQuery { MaxResults = 10 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);
		result.Count().ShouldBe(2);
	}

	[Fact]
	public async Task QueryEventsWithNoFilter()
	{
		// Arrange
		var events = new[]
		{
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess),
			CreateSecurityEvent(SecurityEventType.AuthorizationSuccess),
			CreateSecurityEvent(SecurityEventType.ValidationFailure),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(3);
	}

	[Fact]
	public async Task QueryEventsFilteredByEventType()
	{
		// Arrange
		var events = new[]
		{
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess),
			CreateSecurityEvent(SecurityEventType.AuthenticationFailure),
			CreateSecurityEvent(SecurityEventType.AuthorizationSuccess),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			EventType = SecurityEventType.AuthenticationSuccess,
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(1);
		result.First().EventType.ShouldBe(SecurityEventType.AuthenticationSuccess);
	}

	[Fact]
	public async Task QueryEventsFilteredByStartTime()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var events = new[]
		{
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-2)),
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-1)),
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess, now),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			StartTime = now.AddMinutes(-90),
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(2);
	}

	[Fact]
	public async Task QueryEventsFilteredByEndTime()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var events = new[]
		{
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-2)),
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess, now.AddHours(-1)),
			CreateSecurityEvent(SecurityEventType.AuthenticationSuccess, now),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			EndTime = now.AddMinutes(-30),
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(2);
	}

	[Fact]
	public async Task QueryEventsRespectMaxResults()
	{
		// Arrange
		var events = Enumerable.Range(0, 10)
			.Select(_ => CreateSecurityEvent(SecurityEventType.AuthenticationSuccess))
			.ToArray();
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 5 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(5);
	}

	[Fact]
	public async Task HandleEmptyEventsList()
	{
		// Act
		await _sut.StoreEventsAsync(Array.Empty<SecurityEvent>(), CancellationToken.None);

		// Assert
		var query = new SecurityEventQuery { MaxResults = 10 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task AccumulateEventsFromMultipleStoreCalls()
	{
		// Arrange
		var events1 = new[] { CreateSecurityEvent(SecurityEventType.AuthenticationSuccess) };
		var events2 = new[] { CreateSecurityEvent(SecurityEventType.AuthenticationFailure) };

		// Act
		await _sut.StoreEventsAsync(events1, CancellationToken.None);
		await _sut.StoreEventsAsync(events2, CancellationToken.None);

		// Assert
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);
		result.Count().ShouldBe(2);
	}

	private static SecurityEvent CreateSecurityEvent(SecurityEventType eventType, DateTimeOffset? timestamp = null)
	{
		return new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = timestamp ?? DateTimeOffset.UtcNow,
			EventType = eventType,
			Description = $"Test event {eventType}",
			Severity = SecuritySeverity.Low,
		};
	}
}
