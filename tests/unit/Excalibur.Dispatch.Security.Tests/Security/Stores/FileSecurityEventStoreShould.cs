// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Unit tests for <see cref="FileSecurityEventStore"/> internal class.
/// Tests file-based audit logging including writing, querying, and validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Stores")]
public sealed class FileSecurityEventStoreShould : IDisposable
{
	private readonly string _tempDirectory;
	private readonly FileSecurityEventStore _sut;

	[RequiresUnreferencedCode("Test constructor")]
	public FileSecurityEventStoreShould()
	{
		_tempDirectory = Path.Combine(Path.GetTempPath(), $"excalibur-test-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDirectory);

		var configuration = CreateConfiguration(_tempDirectory);
		_sut = new FileSecurityEventStore(
			NullLogger<FileSecurityEventStore>.Instance,
			configuration);
	}

	public void Dispose()
	{
		_sut.Dispose();

		try
		{
			if (Directory.Exists(_tempDirectory))
			{
				Directory.Delete(_tempDirectory, recursive: true);
			}
		}
		catch
		{
			// Best effort cleanup
		}
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
		typeof(FileSecurityEventStore).IsNotPublic.ShouldBeTrue();
		typeof(FileSecurityEventStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		var configuration = CreateConfiguration(_tempDirectory);

		Should.Throw<ArgumentNullException>(() =>
			new FileSecurityEventStore(null!, configuration));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	public void Constructor_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new FileSecurityEventStore(
				NullLogger<FileSecurityEventStore>.Instance,
				null!));
	}

	[Fact]
	public async Task StoreEventsAsync_ThrowsArgumentNullException_WhenEventsIsNull()
	{
		// ArgumentNullException.ThrowIfNull fires before the semaphore/try-catch block
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
	public async Task StoreEventsAsync_WritesEventsToFile()
	{
		// Arrange
		var events = new[]
		{
			CreateValidEvent(SecurityEventType.AuthenticationSuccess),
			CreateValidEvent(SecurityEventType.AuthorizationFailure),
		};

		// Act
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Assert - verify files were created in temp directory
		var files = Directory.GetFiles(_tempDirectory, "security-audit-*.jsonl");
		files.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task StoreEventsAsync_ThrowsInvalidOperationException_WhenAllEventsAreInvalid()
	{
		// Arrange - event with empty Id and empty Description
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.Empty,
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
	public async Task StoreAndQueryEventsAsync_RoundTripsEvents()
	{
		// Arrange
		var events = new[]
		{
			CreateValidEvent(SecurityEventType.AuthenticationSuccess),
			CreateValidEvent(SecurityEventType.AuthorizationFailure),
		};

		// Act
		await _sut.StoreEventsAsync(events, CancellationToken.None);
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(2);
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsArgumentNullException_WhenQueryIsNull()
	{
		// ArgumentNullException.ThrowIfNull fires before the semaphore/try-catch block
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.QueryEventsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenMaxResultsIsZero()
	{
		var query = new SecurityEventQuery { MaxResults = 0 };

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.QueryEventsAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenMaxResultsIsNegative()
	{
		var query = new SecurityEventQuery { MaxResults = -1 };

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.QueryEventsAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ThrowsInvalidOperationException_WhenStartTimeAfterEndTime()
	{
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow,
			EndTime = DateTimeOffset.UtcNow.AddHours(-1),
			MaxResults = 10,
		};

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.QueryEventsAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryEventsAsync_ReturnsEmpty_WhenNoEventsStored()
	{
		var query = new SecurityEventQuery { MaxResults = 10 };

		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_FiltersByEventType()
	{
		// Arrange
		var events = new[]
		{
			CreateValidEvent(SecurityEventType.AuthenticationSuccess),
			CreateValidEvent(SecurityEventType.AuthenticationFailure),
			CreateValidEvent(SecurityEventType.AuthorizationSuccess),
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
	public async Task QueryEventsAsync_RespectsMaxResults()
	{
		// Arrange
		var events = Enumerable.Range(0, 10)
			.Select(_ => CreateValidEvent(SecurityEventType.AuthenticationSuccess))
			.ToArray();
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 5 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.Count().ShouldBe(5);
	}

	[Fact]
	public async Task QueryEventsAsync_FiltersByStartTime()
	{
		// Arrange - store events with default (current) timestamps
		var events = new[]
		{
			CreateValidEvent(SecurityEventType.AuthenticationSuccess),
			CreateValidEvent(SecurityEventType.AuthorizationSuccess),
		};
		await _sut.StoreEventsAsync(events, CancellationToken.None);

		// Act - query with future StartTime (still today's date so file is included)
		// but after current events so MatchesQuery filters them out
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddMinutes(30),
			MaxResults = 100,
		};
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert - events with current timestamp should be excluded by future StartTime
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task StoreEventsAsync_AccumulatesFromMultipleCalls()
	{
		// Arrange & Act
		await _sut.StoreEventsAsync(
			[CreateValidEvent(SecurityEventType.AuthenticationSuccess)],
			CancellationToken.None);
		await _sut.StoreEventsAsync(
			[CreateValidEvent(SecurityEventType.AuthenticationFailure)],
			CancellationToken.None);

		// Assert
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = await _sut.QueryEventsAsync(query, CancellationToken.None);
		result.Count().ShouldBe(2);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Act & Assert - should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	[RequiresUnreferencedCode("Test")]
	private static IConfiguration CreateConfiguration(string directory)
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:FileStore:Directory"] = directory,
				["Security:Auditing:FileStore:FilePrefix"] = "security-audit",
				["Security:Auditing:FileStore:MaxFileSizeBytes"] = "104857600",
				["Security:Auditing:FileStore:MaxFiles"] = "10",
			})
			.Build();
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
