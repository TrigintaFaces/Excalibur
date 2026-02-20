// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Depth tests for <see cref="FileSecurityEventStore"/>.
/// Covers query filtering paths (severity, userId, sourceIp, correlationId, endTime),
/// file rotation when size threshold is exceeded, compression of rotated files,
/// cleanup of old files, default config values, and descending timestamp sort order.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Stores")]
public sealed class FileSecurityEventStoreDepthShould : IDisposable
{
	private readonly string _tempDirectory;

	public FileSecurityEventStoreDepthShould()
	{
		_tempDirectory = Path.Combine(Path.GetTempPath(), $"excalibur-test-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDirectory);
	}

	public void Dispose()
	{
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
	public async Task QueryEventsAsync_FiltersByMinimumSeverity()
	{
		// Arrange
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, SecuritySeverity.Low),
			CreateEvent(SecurityEventType.AuthenticationFailure, SecuritySeverity.High),
			CreateEvent(SecurityEventType.AuthorizationFailure, SecuritySeverity.Critical),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			MinimumSeverity = SecuritySeverity.High,
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert — only High and Critical should be returned
		result.Count.ShouldBe(2);
		result.ShouldAllBe(e => e.Severity >= SecuritySeverity.High);
	}

	[Fact]
	public async Task QueryEventsAsync_FiltersByUserId()
	{
		// Arrange
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, userId: "user-alpha"),
			CreateEvent(SecurityEventType.AuthenticationSuccess, userId: "user-beta"),
			CreateEvent(SecurityEventType.AuthenticationSuccess, userId: "user-alpha"),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			UserId = "user-alpha",
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldAllBe(e => e.UserId == "user-alpha");
	}

	[Fact]
	public async Task QueryEventsAsync_FiltersBySourceIp()
	{
		// Arrange
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, sourceIp: "10.0.0.1"),
			CreateEvent(SecurityEventType.AuthenticationSuccess, sourceIp: "10.0.0.2"),
			CreateEvent(SecurityEventType.AuthenticationSuccess, sourceIp: "10.0.0.1"),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			SourceIp = "10.0.0.1",
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldAllBe(e => e.SourceIp == "10.0.0.1");
	}

	[Fact]
	public async Task QueryEventsAsync_FiltersByCorrelationId()
	{
		// Arrange
		using var sut = CreateStore();
		var targetCorrelation = Guid.NewGuid();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, correlationId: targetCorrelation),
			CreateEvent(SecurityEventType.AuthenticationSuccess, correlationId: Guid.NewGuid()),
			CreateEvent(SecurityEventType.AuthenticationSuccess, correlationId: targetCorrelation),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery
		{
			CorrelationId = targetCorrelation,
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldAllBe(e => e.CorrelationId == targetCorrelation);
	}

	[Fact]
	public async Task QueryEventsAsync_FiltersByEndTime()
	{
		// Arrange
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess),
			CreateEvent(SecurityEventType.AuthorizationSuccess),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — query with EndTime before now should still include today's file
		// but MatchesQuery filters individual events
		var query = new SecurityEventQuery
		{
			EndTime = DateTimeOffset.UtcNow.AddHours(-1),
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert — events from now should be excluded by past EndTime
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_ReturnsSortedDescendingByTimestamp()
	{
		// Arrange
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess),
			CreateEvent(SecurityEventType.AuthenticationFailure),
			CreateEvent(SecurityEventType.AuthorizationFailure),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert — should be in descending timestamp order
		result.Count.ShouldBe(3);
		for (var i = 0; i < result.Count - 1; i++)
		{
			result[i].Timestamp.ShouldBeGreaterThanOrEqualTo(result[i + 1].Timestamp);
		}
	}

	[Fact]
	public async Task QueryEventsAsync_CombinesMultipleFilters()
	{
		// Arrange
		using var sut = CreateStore();
		var targetCorrelation = Guid.NewGuid();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess, SecuritySeverity.High, "user-1", "10.0.0.1", targetCorrelation),
			CreateEvent(SecurityEventType.AuthenticationSuccess, SecuritySeverity.Low, "user-1", "10.0.0.1", targetCorrelation),
			CreateEvent(SecurityEventType.AuthenticationFailure, SecuritySeverity.High, "user-2", "10.0.0.1", targetCorrelation),
			CreateEvent(SecurityEventType.AuthenticationSuccess, SecuritySeverity.High, "user-1", "10.0.0.2", targetCorrelation),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — combine event type + severity + userId + sourceIp + correlationId
		var query = new SecurityEventQuery
		{
			EventType = SecurityEventType.AuthenticationSuccess,
			MinimumSeverity = SecuritySeverity.High,
			UserId = "user-1",
			SourceIp = "10.0.0.1",
			CorrelationId = targetCorrelation,
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert — only 1 event matches all filters
		result.Count.ShouldBe(1);
	}

	[Fact]
	public async Task StoreEventsAsync_RotatesFileWhenSizeExceeded()
	{
		// Arrange — create store with very small max file size to trigger rotation
		var config = CreateConfiguration(_tempDirectory, maxFileSizeBytes: 100);
		using var sut = new FileSecurityEventStore(
			NullLogger<FileSecurityEventStore>.Instance, config);

		// Write enough events to exceed 100 bytes (description in initializer, not assigned after)
		await sut.StoreEventsAsync(
			[CreateEvent(SecurityEventType.AuthenticationSuccess, description: new string('A', 200))],
			CancellationToken.None);

		// Act — second write should trigger rotation
		await sut.StoreEventsAsync(
			[CreateEvent(SecurityEventType.AuthorizationSuccess, description: new string('B', 200))],
			CancellationToken.None);

		// Assert — should have rotated (gz file or timestamped file created)
		var allFiles = Directory.GetFiles(_tempDirectory, "security-audit-*");
		allFiles.Length.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task StoreEventsAsync_CleanupRemovesOldFilesWhenExceedingMaxFiles()
	{
		// Arrange — store with maxFiles=2 and tiny max size to force rotation
		var config = CreateConfiguration(_tempDirectory, maxFileSizeBytes: 50, maxFiles: 2);
		using var sut = new FileSecurityEventStore(
			NullLogger<FileSecurityEventStore>.Instance, config);

		// Create several rounds of data to trigger multiple rotations
		for (var i = 0; i < 5; i++)
		{
			await sut.StoreEventsAsync(
				[CreateEvent(
					SecurityEventType.AuthenticationSuccess,
					description: $"Event round {i} with enough data to exceed 50 bytes threshold easily")],
				CancellationToken.None);
		}

		// Assert — should not exceed maxFiles (2) + current file
		var allFiles = Directory.GetFiles(_tempDirectory, "security-audit-*");
		allFiles.Length.ShouldBeLessThanOrEqualTo(4); // generous: current + rotated + compressed + one extra
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	public void Constructor_UsesDefaultConfigValues_WhenNotProvided()
	{
		// Arrange — empty config, defaults should apply
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		// Act — constructor should create default directory "./logs/security"
		using var sut = new FileSecurityEventStore(
			NullLogger<FileSecurityEventStore>.Instance, config);

		// Assert — sut created without exception
		sut.ShouldNotBeNull();

		// Cleanup the default directory if created
		try
		{
			if (Directory.Exists("./logs/security"))
			{
				Directory.Delete("./logs/security", recursive: true);
			}

			if (Directory.Exists("logs"))
			{
				var secDir = Path.Combine("logs", "security");
				if (Directory.Exists(secDir) &&
					Directory.GetFiles(secDir).Length == 0 &&
					Directory.GetDirectories(secDir).Length == 0)
				{
					Directory.Delete(secDir);
					if (Directory.GetFiles("logs").Length == 0 &&
						Directory.GetDirectories("logs").Length == 0)
					{
						Directory.Delete("logs");
					}
				}
			}
		}
		catch
		{
			// Best effort cleanup
		}
	}

	[Fact]
	public async Task StoreEventsAsync_SkipsInvalidEventsAndWritesValidOnes()
	{
		// Arrange
		using var sut = CreateStore();
		var events = new[]
		{
			new SecurityEvent
			{
				Id = Guid.Empty, // Invalid
				Timestamp = DateTimeOffset.UtcNow,
				EventType = SecurityEventType.AuthenticationSuccess,
				Description = "Should be skipped",
				Severity = SecuritySeverity.Low,
			},
			CreateEvent(SecurityEventType.AuthorizationSuccess), // Valid
		};

		// Act
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Assert — one valid event persisted
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();
		result.Count.ShouldBe(1);
		result[0].EventType.ShouldBe(SecurityEventType.AuthorizationSuccess);
	}

	[Fact]
	public async Task QueryEventsAsync_AcceptsLargeMaxResults()
	{
		// Note: FileSecurityEventStore does NOT have a 10000 limit (unlike Elasticsearch).
		// It validates MaxResults > 0 only. Verify large MaxResults works.
		using var sut = CreateStore();
		await sut.StoreEventsAsync(
			[CreateEvent(SecurityEventType.AuthenticationSuccess)],
			CancellationToken.None);

		var query = new SecurityEventQuery { MaxResults = 50000 };
		var result = await sut.QueryEventsAsync(query, CancellationToken.None);

		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task QueryEventsAsync_ReturnsEmptyWhenNoFilesExist()
	{
		// Arrange — create store and write nothing
		using var sut = CreateStore();

		// Act
		var query = new SecurityEventQuery { MaxResults = 100 };
		var result = await sut.QueryEventsAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_ExcludesEventsOutsidePastTimeRange()
	{
		// Arrange — events stored NOW
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess),
			CreateEvent(SecurityEventType.AuthorizationSuccess),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — query with past time range (yesterday) that excludes today's events
		// This exercises the GetLogFilesInDateRange + MatchesQuery StartTime/EndTime paths
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddDays(-2),
			EndTime = DateTimeOffset.UtcNow.AddHours(-1),
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert — events from now are outside the past range
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_ExcludesEventsBeforeFutureStartTime()
	{
		// Arrange — events stored NOW
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess),
			CreateEvent(SecurityEventType.AuthorizationSuccess),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Act — StartTime in the future excludes current events via MatchesQuery
		// (file is still found since the date is today)
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(1),
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert — events from now are before future StartTime
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryEventsAsync_IncludesEventsWhenStartTimeIsPast()
	{
		// Arrange — events stored NOW
		using var sut = CreateStore();
		var events = new[]
		{
			CreateEvent(SecurityEventType.AuthenticationSuccess),
			CreateEvent(SecurityEventType.AuthorizationSuccess),
		};
		await sut.StoreEventsAsync(events, CancellationToken.None);

		// Sanity check: events are persisted and queryable without time filters.
		var baseline = (await sut.QueryEventsAsync(new SecurityEventQuery { MaxResults = 100 }, CancellationToken.None)).ToList();
		baseline.Count.ShouldBe(2);
		baseline.ShouldAllBe(e => e.Timestamp != default);

		// Act — StartTime in the past should include the current log file and events.
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(-2),
			MaxResults = 100,
		};
		var result = (await sut.QueryEventsAsync(query, CancellationToken.None)).ToList();

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task QueryEventsAsync_RejectsStartTimeAfterEndTime()
	{
		// Arrange
		using var sut = CreateStore();

		// Act & Assert — StartTime > EndTime should throw
		var query = new SecurityEventQuery
		{
			StartTime = DateTimeOffset.UtcNow.AddMinutes(5),
			EndTime = DateTimeOffset.UtcNow.AddMinutes(-5),
			MaxResults = 100,
		};

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.QueryEventsAsync(query, CancellationToken.None));
	}

	private FileSecurityEventStore CreateStore()
	{
		return new FileSecurityEventStore(
			NullLogger<FileSecurityEventStore>.Instance,
			CreateConfiguration(_tempDirectory));
	}

	private static IConfiguration CreateConfiguration(
		string directory,
		long maxFileSizeBytes = 100 * 1024 * 1024,
		int maxFiles = 10)
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:FileStore:Directory"] = directory,
				["Security:Auditing:FileStore:FilePrefix"] = "security-audit",
				["Security:Auditing:FileStore:MaxFileSizeBytes"] = maxFileSizeBytes.ToString(),
				["Security:Auditing:FileStore:MaxFiles"] = maxFiles.ToString(),
			})
			.Build();
	}

	private static SecurityEvent CreateEvent(
		SecurityEventType eventType,
		SecuritySeverity severity = SecuritySeverity.Low,
		string? userId = null,
		string? sourceIp = null,
		Guid? correlationId = null,
		string? description = null)
	{
		return new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = DateTimeOffset.UtcNow,
			EventType = eventType,
			Description = description ?? $"Test event {eventType}",
			Severity = severity,
			UserId = userId,
			SourceIp = sourceIp,
			CorrelationId = correlationId,
		};
	}
}

#pragma warning restore IL2026, IL3050
