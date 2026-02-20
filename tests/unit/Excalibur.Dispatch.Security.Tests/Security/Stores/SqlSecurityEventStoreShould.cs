// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Unit tests for <see cref="SqlSecurityEventStore"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Stores")]
public sealed class SqlSecurityEventStoreShould : IDisposable
{
	private readonly SqlSecurityEventStore _sut;

	public SqlSecurityEventStoreShould()
	{
		_sut = new SqlSecurityEventStore(NullLogger<SqlSecurityEventStore>.Instance);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	[Fact]
	public void ImplementISecurityEventStore()
	{
		_sut.ShouldBeAssignableTo<ISecurityEventStore>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		typeof(SqlSecurityEventStore).IsNotPublic.ShouldBeTrue();
		typeof(SqlSecurityEventStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlSecurityEventStore(null!));
	}

	[Fact]
	public async Task StoreEventsAsync_ThrowsArgumentNullException_WhenEventsIsNull()
	{
		// ArgumentNullException.ThrowIfNull fires before the try-catch block
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.StoreEventsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task StoreEventsAsync_CompletesSuccessfully_WhenEventsIsEmpty()
	{
		// Empty list should return immediately without error
		await _sut.StoreEventsAsync(Array.Empty<SecurityEvent>(), CancellationToken.None);
	}

	[Fact]
	public async Task StoreEventsAsync_CompletesSuccessfully_WhenAllEventsAreValid()
	{
		// Arrange
		var events = new[]
		{
			CreateValidEvent(SecurityEventType.AuthenticationSuccess),
			CreateValidEvent(SecurityEventType.AuthorizationFailure),
		};

		// Act & Assert - should not throw
		await _sut.StoreEventsAsync(events, CancellationToken.None);
	}

	[Fact]
	public async Task StoreEventsAsync_ThrowsInvalidOperationException_WhenEventHasEmptyId()
	{
		// Arrange
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.Empty,
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityEventType.AuthenticationSuccess,
				Description = "Test event",
				Severity = SecuritySeverity.Low,
			},
		};

		// Act & Assert - invalid event causes ArgumentException wrapped in InvalidOperationException
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.StoreEventsAsync(events, CancellationToken.None));
	}

	[Fact]
	public async Task StoreEventsAsync_ThrowsInvalidOperationException_WhenEventHasEmptyDescription()
	{
		// Arrange
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.NewGuid(),
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityEventType.AuthenticationSuccess,
				Description = "",
				Severity = SecuritySeverity.Low,
			},
		};

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.StoreEventsAsync(events, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsArgumentNullException_WhenQueryIsNull()
	{
		// ArgumentNullException.ThrowIfNull fires before the try-catch block
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.QueryEventsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenMaxResultsIsZero()
	{
		// Arrange
		var query = new SecurityEventQuery { MaxResults = 0 };

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.QueryEventsAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenMaxResultsIsNegative()
	{
		// Arrange
		var query = new SecurityEventQuery { MaxResults = -1 };

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.QueryEventsAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenStartTimeAfterEndTime()
	{
		// Arrange
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow,
			EndTime = DateTimeOffset.UtcNow.AddHours(-1),
			MaxResults = 10,
		};

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.QueryEventsAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ReturnsEmptyCollection_WithValidQuery()
	{
		// Arrange - SQL store returns empty without real DB
		var query = new SecurityEventQuery { MaxResults = 10 };

		// Act
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	private static SecurityEvent CreateValidEvent(SecurityEventType eventType)
	{
		return new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = DateTimeOffset.UtcNow,
			EventType = eventType,
			Description = $"Test event {eventType}",
			Severity = SecuritySeverity.Low,
		};
	}
}
