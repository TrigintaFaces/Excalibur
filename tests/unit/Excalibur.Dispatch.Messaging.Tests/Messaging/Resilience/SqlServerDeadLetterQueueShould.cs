// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Unit tests (bd-fiyr) for <see cref="SqlServerDeadLetterQueue"/> and related components.
/// Tests configuration, options, and behavior without requiring a real database.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerDeadLetterQueueShould
{
	private readonly ILogger<SqlServerDeadLetterQueue> _logger;

	public SqlServerDeadLetterQueueShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<SqlServerDeadLetterQueue>();
	}

	#region Options Tests

	[Fact]
	public void OptionsHaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions();

		// Assert
		options.ConnectionString.ShouldBeEmpty();
		options.TableName.ShouldBe("DeadLetterQueue");
		options.SchemaName.ShouldBe("dbo");
		options.CommandTimeoutSeconds.ShouldBe(30);
		options.DefaultRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void OptionsGenerateCorrectQualifiedTableName()
	{
		// Arrange
		var options = new SqlServerDeadLetterQueueOptions
		{
			SchemaName = "messaging",
			TableName = "DLQ"
		};

		// Act
		var qualifiedName = options.QualifiedTableName;

		// Assert
		qualifiedName.ShouldBe("[messaging].[DLQ]");
	}

	[Fact]
	public void OptionsAllowCustomConnectionString()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb;Trusted_Connection=true;";

		// Act
		var options = new SqlServerDeadLetterQueueOptions
		{
			ConnectionString = connectionString
		};

		// Assert
		options.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void OptionsAllowCustomCommandTimeout()
	{
		// Arrange & Act
		var options = new SqlServerDeadLetterQueueOptions
		{
			CommandTimeoutSeconds = 120
		};

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(120);
	}

	[Fact]
	public void OptionsAllowCustomRetentionPeriod()
	{
		// Arrange
		var retention = TimeSpan.FromDays(90);

		// Act
		var options = new SqlServerDeadLetterQueueOptions
		{
			DefaultRetentionPeriod = retention
		};

		// Assert
		options.DefaultRetentionPeriod.ShouldBe(retention);
	}

	#endregion Options Tests

	#region DeadLetterEntry Tests

	[Fact]
	public void DeadLetterEntryHasCorrectProperties()
	{
		// Arrange
		var id = Guid.NewGuid();
		var enqueuedAt = DateTimeOffset.UtcNow;
		var payload = new byte[] { 1, 2, 3 };

		// Act
		var entry = new DeadLetterEntry
		{
			Id = id,
			MessageType = "TestMessage",
			Payload = payload,
			Reason = DeadLetterReason.MaxRetriesExceeded,
			ExceptionMessage = "Test error",
			ExceptionStackTrace = "at Test.Method()",
			EnqueuedAt = enqueuedAt,
			OriginalAttempts = 5,
			Metadata = new Dictionary<string, string> { ["Key"] = "Value" },
			CorrelationId = "corr-123",
			CausationId = "caus-456",
			SourceQueue = "test-queue",
			IsReplayed = false,
			ReplayedAt = null
		};

		// Assert
		entry.Id.ShouldBe(id);
		entry.MessageType.ShouldBe("TestMessage");
		entry.Payload.ShouldBe(payload);
		entry.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		entry.ExceptionMessage.ShouldBe("Test error");
		entry.ExceptionStackTrace.ShouldBe("at Test.Method()");
		entry.EnqueuedAt.ShouldBe(enqueuedAt);
		entry.OriginalAttempts.ShouldBe(5);
		entry.Metadata["Key"].ShouldBe("Value");
		entry.CorrelationId.ShouldBe("corr-123");
		entry.CausationId.ShouldBe("caus-456");
		entry.SourceQueue.ShouldBe("test-queue");
		entry.IsReplayed.ShouldBeFalse();
		entry.ReplayedAt.ShouldBeNull();
	}

	[Fact]
	public void DeadLetterEntryTracksReplayState()
	{
		// Arrange
		var replayedAt = DateTimeOffset.UtcNow;

		// Act
		var entry = new DeadLetterEntry
		{
			Id = Guid.NewGuid(),
			MessageType = "TestMessage",
			Payload = Array.Empty<byte>(),
			Reason = DeadLetterReason.MaxRetriesExceeded,
			EnqueuedAt = DateTimeOffset.UtcNow.AddHours(-1),
			OriginalAttempts = 3,
			IsReplayed = true,
			ReplayedAt = replayedAt
		};

		// Assert
		entry.IsReplayed.ShouldBeTrue();
		entry.ReplayedAt.ShouldBe(replayedAt);
	}

	#endregion DeadLetterEntry Tests

	#region DeadLetterQueryFilter Tests

	[Fact]
	public void QueryFilterSupportsMessageTypeFiltering()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter
		{
			MessageType = "Order"
		};

		// Assert
		filter.MessageType.ShouldBe("Order");
	}

	[Fact]
	public void QueryFilterSupportsReasonFiltering()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter
		{
			Reason = DeadLetterReason.CircuitBreakerOpen
		};

		// Assert
		filter.Reason.ShouldBe(DeadLetterReason.CircuitBreakerOpen);
	}

	[Fact]
	public void QueryFilterSupportsDateRangeFiltering()
	{
		// Arrange
		var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
		var toDate = DateTimeOffset.UtcNow;

		// Act
		var filter = new DeadLetterQueryFilter
		{
			FromDate = fromDate,
			ToDate = toDate
		};

		// Assert
		filter.FromDate.ShouldBe(fromDate);
		filter.ToDate.ShouldBe(toDate);
	}

	[Fact]
	public void QueryFilterSupportsReplayedFiltering()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter
		{
			IsReplayed = false
		};

		// Assert
		filter.IsReplayed.ShouldBe(false);
	}

	[Fact]
	public void QueryFilterSupportsSourceQueueFiltering()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter
		{
			SourceQueue = "orders-queue"
		};

		// Assert
		filter.SourceQueue.ShouldBe("orders-queue");
	}

	[Fact]
	public void QueryFilterSupportsCorrelationIdFiltering()
	{
		// Arrange
		var correlationId = Guid.NewGuid().ToString();

		// Act
		var filter = new DeadLetterQueryFilter
		{
			CorrelationId = correlationId
		};

		// Assert
		filter.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void QueryFilterSupportsMinAttemptsFiltering()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter
		{
			MinAttempts = 5
		};

		// Assert
		filter.MinAttempts.ShouldBe(5);
	}

	[Fact]
	public void QueryFilterSupportsPagination()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter
		{
			Skip = 20
		};

		// Assert
		filter.Skip.ShouldBe(20);
	}

	#endregion DeadLetterQueryFilter Tests

	#region DeadLetterReason Tests

	[Theory]
	[InlineData(DeadLetterReason.MaxRetriesExceeded)]
	[InlineData(DeadLetterReason.CircuitBreakerOpen)]
	[InlineData(DeadLetterReason.DeserializationFailed)]
	[InlineData(DeadLetterReason.HandlerNotFound)]
	[InlineData(DeadLetterReason.ValidationFailed)]
	[InlineData(DeadLetterReason.ManualRejection)]
	[InlineData(DeadLetterReason.MessageExpired)]
	[InlineData(DeadLetterReason.AuthorizationFailed)]
	[InlineData(DeadLetterReason.UnhandledException)]
	[InlineData(DeadLetterReason.PoisonMessage)]
	[InlineData(DeadLetterReason.Unknown)]
	public void DeadLetterReasonEnumContainsAllExpectedValues(DeadLetterReason reason)
	{
		// Arrange & Act & Assert
		Enum.IsDefined(reason).ShouldBeTrue();
	}

	[Fact]
	public void DeadLetterReasonHasCorrectCount()
	{
		// Arrange & Act
		var values = Enum.GetValues<DeadLetterReason>();

		// Assert - 11 known reasons
		values.Length.ShouldBe(11);
	}

	#endregion DeadLetterReason Tests

	#region Constructor Validation Tests

	[Fact]
	public void ThrowsWhenOptionsIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerDeadLetterQueue(null!, _logger));
	}

	[Fact]
	public void ThrowsWhenLoggerIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerDeadLetterQueueOptions
		{
			ConnectionString = "Server=localhost;Database=Test;"
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerDeadLetterQueue(options, null!));
	}

	[Fact]
	public void AcceptsValidConstructorParameters()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerDeadLetterQueueOptions
		{
			ConnectionString = "Server=localhost;Database=Test;"
		});

		// Act
		var queue = new SqlServerDeadLetterQueue(options, _logger);

		// Assert
		_ = queue.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptsOptionalReplayHandler()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerDeadLetterQueueOptions
		{
			ConnectionString = "Server=localhost;Database=Test;"
		});
		Func<object, Task> replayHandler = async obj => await Task.CompletedTask.ConfigureAwait(false);

		// Act
		var queue = new SqlServerDeadLetterQueue(options, _logger, replayHandler);

		// Assert
		_ = queue.ShouldNotBeNull();
	}

	#endregion Constructor Validation Tests

	#region IDeadLetterQueue Interface Tests

	[Fact]
	public void ImplementsIDeadLetterQueueInterface()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerDeadLetterQueueOptions
		{
			ConnectionString = "Server=localhost;Database=Test;"
		});

		// Act
		var queue = new SqlServerDeadLetterQueue(options, _logger);

		// Assert
		_ = queue.ShouldBeAssignableTo<IDeadLetterQueue>();
	}

	#endregion IDeadLetterQueue Interface Tests
}
