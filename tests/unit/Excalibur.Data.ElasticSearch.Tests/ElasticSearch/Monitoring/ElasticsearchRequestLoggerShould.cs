// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for <see cref="ElasticsearchRequestLogger"/>, exercised through the seam a consumer actually configures.
/// </summary>
/// <remarks>
/// These assert on what reaches the logging sink rather than on the redactor in isolation, so that a body reaching the
/// sink unredacted fails here even if the redactor itself is correct but is not wired into the logging path.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class ElasticsearchRequestLoggerShould
{
	private const string OperationType = "Index";

	private static (ElasticsearchRequestLogger Logger, CapturingLogger Sink) CreateLogger(RequestLoggingOptions logging)
	{
		var sink = new CapturingLogger();
		var options = Options.Create(new ElasticsearchMonitoringOptions { RequestLogging = logging });
		return (new ElasticsearchRequestLogger(sink, options), sink);
	}

	private static RequestLoggingOptions BodyLogging(
		IEnumerable<string>? allowed = null,
		int maxBodySizeBytes = 4096) =>
		new()
		{
			Enabled = true,
			LogRequestBody = true,
			MaxBodySizeBytes = maxBodySizeBytes,
			AllowedBodyProperties = new HashSet<string>(allowed ?? [], StringComparer.OrdinalIgnoreCase),
		};

	#region Safety - an unlisted value must not reach the sink

	[Fact]
	public void NotWriteAnUnlistedBodyValueToTheSink()
	{
		// Arrange - a field the previous deny list never masked
		var (logger, sink) = CreateLogger(BodyLogging());

		// Act
		logger.LogRequest(OperationType, new { ssn = "123-45-6789" });

		// Assert
		sink.RequestBody().ShouldNotContain("123-45-6789");
	}

	[Fact]
	public void NotWriteAnUnlistedBodyValueEvenWhenAnotherPropertyIsAllowed()
	{
		// Arrange
		var (logger, sink) = CreateLogger(BodyLogging(["index"]));

		// Act
		logger.LogRequest(OperationType, new { index = "orders", clientSecret = "top-secret" });

		// Assert
		sink.RequestBody().ShouldNotContain("top-secret");
	}

	[Fact]
	public void NotWriteBodyContentWhenRequestBodyLoggingIsOff()
	{
		// Arrange
		var (logger, sink) = CreateLogger(new RequestLoggingOptions { Enabled = true, LogRequestBody = false });

		// Act
		logger.LogRequest(OperationType, new { ssn = "123-45-6789" });

		// Assert
		sink.LogData().ShouldNotContainKey("RequestBody");
	}

	[Fact]
	public void NotWriteBodyContentWhenTheConfiguredSizeLeavesNoRoom()
	{
		// Arrange - startup validation rejects a non-positive limit, so this is the belt-and-braces case: the logger is
		// constructible directly, without ever passing through that validation.
		var (logger, sink) = CreateLogger(BodyLogging(maxBodySizeBytes: 0));

		// Act
		logger.LogRequest(OperationType, new { ssn = "123-45-6789" });

		// Assert
		sink.LogData().ShouldNotContainKey("RequestBody");
	}

	#endregion

	#region Liveness - logging must still happen, and allowed values must still appear

	[Fact]
	public void WriteAnAllowListedBodyValueToTheSink()
	{
		// Arrange
		var (logger, sink) = CreateLogger(BodyLogging(["index"]));

		// Act
		logger.LogRequest(OperationType, new { index = "orders", clientSecret = "top-secret" });

		// Assert
		sink.RequestBody().ShouldContain("orders");
	}

	[Fact]
	public void StillLogTheRequestWithItsStructureIntact()
	{
		// Arrange - guards against a redactor that passes every safety assertion by logging nothing
		var (logger, sink) = CreateLogger(BodyLogging());

		// Act
		logger.LogRequest(OperationType, new { index = "orders", ssn = "123-45-6789" });

		// Assert
		sink.Entries.ShouldNotBeEmpty();
		var body = sink.RequestBody();
		body.ShouldNotBeNullOrWhiteSpace();
		body.ShouldContain("index");
		body.ShouldContain("ssn");
	}

	[Fact]
	public void StillRecordTheSurroundingOperationMetadata()
	{
		// Arrange
		var (logger, sink) = CreateLogger(BodyLogging());

		// Act
		logger.LogRequest(OperationType, new { ssn = "123-45-6789" }, indexName: "orders");

		// Assert
		var logData = sink.LogData();
		logData["OperationType"].ShouldBe(OperationType);
		logData["IndexName"].ShouldBe("orders");
	}

	#endregion

	#region Size bounding

	[Fact]
	public void BoundTheRedactedBodyToTheConfiguredSize()
	{
		// Arrange
		var (logger, sink) = CreateLogger(BodyLogging(maxBodySizeBytes: 40));

		// Act
		logger.LogRequest(OperationType, new { first = new string('a', 500), second = new string('b', 500) });

		// Assert
		sink.RequestBody().Length.ShouldBeLessThanOrEqualTo(40);
	}

	[Fact]
	public void NotFailWhenTheConfiguredSizeIsSmallerThanTheTruncationMarker()
	{
		// Arrange - a size below the marker length used to index past the start of the string and throw
		var (logger, sink) = CreateLogger(BodyLogging(maxBodySizeBytes: 5));

		// Act
		logger.LogRequest(OperationType, new { first = new string('a', 500) });

		// Assert - the operation is still logged, and nothing was swallowed as a logging failure
		sink.Entries.ShouldNotBeEmpty();
		sink.Errors.ShouldBeEmpty();
		sink.RequestBody().Length.ShouldBeLessThanOrEqualTo(5);
	}

	#endregion

	/// <summary>
	/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was handed so a test can assert on it.
	/// </summary>
	private sealed class CapturingLogger : ILogger<ElasticsearchRequestLogger>
	{
		public List<IReadOnlyList<KeyValuePair<string, object?>>> Entries { get; } = [];

		public List<Exception> Errors { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (exception is not null)
			{
				Errors.Add(exception);
			}

			if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
			{
				Entries.Add(values);
			}
		}

		/// <summary> Gets the structured payload of the single captured entry. </summary>
		public Dictionary<string, object> LogData()
		{
			Entries.Count.ShouldBe(1, "expected exactly one log entry to assert against");

			// The message template destructures the payload, so the captured name carries the sigil.
			var payload = Entries[0]
				.First(pair => pair.Key.EndsWith("LogData", StringComparison.Ordinal))
				.Value;
			return payload.ShouldBeOfType<Dictionary<string, object>>();
		}

		/// <summary> Gets the request body recorded on the single captured entry. </summary>
		public string RequestBody()
		{
			var logData = LogData();
			logData.ShouldContainKey("RequestBody");
			return logData["RequestBody"].ShouldBeOfType<string>();
		}
	}
}
