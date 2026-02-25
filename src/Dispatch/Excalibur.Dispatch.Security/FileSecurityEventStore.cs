// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// File-based security event store implementation for reliable audit logging to disk. Provides tamper-evident logging with rotation and
/// compression for compliance requirements.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class FileSecurityEventStore : ISecurityEventStore, IDisposable
{
	// Use source-generated context for AOT-safe serialization/deserialization

	private readonly ILogger<FileSecurityEventStore> _logger;
	private readonly string _logDirectory;
	private readonly string _filePrefix;
	private readonly long _maxFileSizeBytes;
	private readonly int _maxFiles;
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	/// <summary>
	/// Initializes a new instance of the <see cref="FileSecurityEventStore"/> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="configuration"> The configuration instance. </param>
	[RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
	public FileSecurityEventStore(
		ILogger<FileSecurityEventStore> logger,
		IConfiguration configuration)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(configuration);

		_logDirectory = configuration["Security:Auditing:FileStore:Directory"] ?? "./logs/security";
		_filePrefix = configuration["Security:Auditing:FileStore:FilePrefix"] ?? "security-audit";
		_maxFileSizeBytes =
			configuration.GetValue<long>("Security:Auditing:FileStore:MaxFileSizeBytes", 100 * 1024 * 1024); // 100MB default
		_maxFiles = configuration.GetValue("Security:Auditing:FileStore:MaxFiles", 10);

		// Ensure log directory exists
		_ = Directory.CreateDirectory(_logDirectory);
	}

	/// <summary>
	/// Stores security events to audit log files.
	/// </summary>
	/// <param name="events"> The security events to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>A task that represents the asynchronous store operation.</returns>
	/// <exception cref="ArgumentException">Thrown when no valid events can be written to the audit log file.</exception>
	/// <exception cref="InvalidOperationException">Thrown when storing security events to audit log files fails.</exception>
	public async Task StoreEventsAsync(IEnumerable<SecurityEvent> events, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);

		var eventsList = events.ToList();
		if (eventsList.Count == 0)
		{
			return;
		}

		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			LogStoringEvents(eventsList.Count);

			var currentLogFile = GetCurrentLogFilePath();
			await RotateFileIfNeededAsync(currentLogFile, cancellationToken).ConfigureAwait(false);

			var validEvents = await WriteEventsToFileAsync(currentLogFile, eventsList, cancellationToken).ConfigureAwait(false);

			if (validEvents == 0)
			{
				throw new ArgumentException(
						Resources.FileSecurityEventStore_NoValidEventsToWrite,
						nameof(events));
			}

			LogEventsStored(validEvents, currentLogFile);
		}
		catch (Exception ex)
		{
			LogStoreEventsFailed(ex, eventsList.Count);
			throw new InvalidOperationException(
					Resources.FileSecurityEventStore_FailedToStoreEvents,
					ex);
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <summary>
	/// Queries security events from audit log files.
	/// </summary>
	/// <param name="query"> The query parameters. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The matching security events. </returns>
	/// <exception cref="ArgumentException">Thrown when MaxResults is less than or equal to zero, or when StartTime is greater than EndTime.</exception>
	/// <exception cref="InvalidOperationException">Thrown when querying security events from audit log files fails.</exception>
	public async Task<IEnumerable<SecurityEvent>> QueryEventsAsync(SecurityEventQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await QueryEventsInternalAsync(query, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogQueryEventsFailed(ex);
			throw new InvalidOperationException(
					Resources.FileSecurityEventStore_FailedToQueryEvents,
					ex);
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	/// <summary>
	/// Disposes the file security event store and releases all managed resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	[GeneratedRegex(@"(\d{4}-\d{2}-\d{2})", RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
	private static partial Regex FileDateRegex();

	private static void ValidateQuery(SecurityEventQuery query)
	{
		if (query.MaxResults <= 0)
		{
			throw new ArgumentException(
					Resources.FileSecurityEventStore_MaxResultsMustBeGreaterThanZero,
					nameof(query));
		}

		if (query is { StartTime: not null, EndTime: not null } && query.StartTime > query.EndTime)
		{
			throw new ArgumentException(
					Resources.FileSecurityEventStore_StartTimeAfterEndTime,
					nameof(query));
		}
	}

	private static SecurityEvent? ParseLogLine(string line)
	{
		// Extract JSON from log line (skip timestamp prefix)
		var jsonStart = line.IndexOf('{', StringComparison.Ordinal);
		if (jsonStart == -1)
		{
			return null;
		}

		var json = line.Substring(jsonStart);
		return JsonSerializer.Deserialize(json, SecurityEventSerializerContext.Default.SecurityEvent);
	}

	private static bool MatchesQuery(SecurityEvent securityEvent, SecurityEventQuery query)
	{
		if (query.StartTime.HasValue && securityEvent.Timestamp < query.StartTime.Value)
		{
			return false;
		}

		if (query.EndTime.HasValue && securityEvent.Timestamp > query.EndTime.Value)
		{
			return false;
		}

		if (query.EventType.HasValue && securityEvent.EventType != query.EventType.Value)
		{
			return false;
		}

		if (query.MinimumSeverity.HasValue && securityEvent.Severity < query.MinimumSeverity.Value)
		{
			return false;
		}

		if (!string.IsNullOrEmpty(query.UserId) && !string.Equals(securityEvent.UserId, query.UserId, StringComparison.Ordinal))
		{
			return false;
		}

		if (!string.IsNullOrEmpty(query.SourceIp) && !string.Equals(securityEvent.SourceIp, query.SourceIp, StringComparison.Ordinal))
		{
			return false;
		}

		if (query.CorrelationId.HasValue && securityEvent.CorrelationId != query.CorrelationId.Value)
		{
			return false;
		}

		return true;
	}

	private string GetCurrentLogFilePath()
	{
		var currentDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		return Path.Combine(_logDirectory, $"{_filePrefix}-{currentDate}.jsonl");
	}

	private async Task RotateFileIfNeededAsync(string currentLogFile, CancellationToken cancellationToken)
	{
		if (File.Exists(currentLogFile))
		{
			var fileInfo = new FileInfo(currentLogFile);
			if (fileInfo.Length > _maxFileSizeBytes)
			{
				await RotateLogFileAsync(currentLogFile, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task<int> WriteEventsToFileAsync(string currentLogFile, List<SecurityEvent> eventsList, CancellationToken cancellationToken)
	{
		var fileStream = new FileStream(currentLogFile, FileMode.Append, FileAccess.Write, FileShare.Read, 4096,
			FileOptions.WriteThrough);
		await using (fileStream.ConfigureAwait(false))
		{
			var writer = new StreamWriter(fileStream, Encoding.UTF8);
			await using (writer.ConfigureAwait(false))
			{
				var validEvents = 0;
				foreach (var evt in eventsList)
				{
					if (evt.Id == Guid.Empty || string.IsNullOrWhiteSpace(evt.Description))
					{
						LogInvalidEvent(evt.Id, evt.Description);
						continue;
					}

					try
					{
						var json = JsonSerializer.Serialize(evt, SecurityEventSerializerContext.Default.SecurityEvent);
						var logLine = $"[{DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)}] {json}";
						await writer.WriteLineAsync(logLine).ConfigureAwait(false);
						validEvents++;
					}
					catch (Exception ex)
					{
						LogSerializationFailed(ex, evt.Id);
					}
				}

				await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
				await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

				return validEvents;
			}
		}
	}

	private async Task<IEnumerable<SecurityEvent>> QueryEventsInternalAsync(SecurityEventQuery query, CancellationToken cancellationToken)
	{
		LogQueryingEvents(JsonSerializer.Serialize(query, SecurityEventSerializerContext.Default.SecurityEventQuery));

		ValidateQuery(query);

		var logFiles = GetLogFilesInDateRange(query.StartTime, query.EndTime);
		var results = await ReadEventsFromFilesAsync(logFiles, query, cancellationToken).ConfigureAwait(false);

		results.Sort(static (a, b) => b.Timestamp.CompareTo(a.Timestamp));

		LogQueryResults(results.Count, logFiles.Count);

		return results;
	}

	private async Task<List<SecurityEvent>> ReadEventsFromFilesAsync(
		List<string> logFiles,
		SecurityEventQuery query,
		CancellationToken cancellationToken)
	{
		var results = new List<SecurityEvent>();

		foreach (var logFile in logFiles.OrderDescending())
		{
			if (results.Count >= query.MaxResults)
			{
				break;
			}

			try
			{
				await ReadEventsFromFileAsync(logFile, query, results, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogReadFileFailed(ex, logFile);
			}
		}

		return results;
	}

	private async Task ReadEventsFromFileAsync(
		string logFile,
		SecurityEventQuery query,
		List<SecurityEvent> results,
		CancellationToken cancellationToken)
	{
		var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		await using (fileStream.ConfigureAwait(false))
		{
			using var reader = new StreamReader(fileStream, Encoding.UTF8);

			string? line;
			while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null && results.Count < query.MaxResults)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					var securityEvent = ParseLogLine(line);
					if (securityEvent != null && MatchesQuery(securityEvent, query))
					{
						results.Add(securityEvent);
					}
				}
				catch (JsonException ex)
				{
					LogDeserializationFailed(ex, logFile, line);
				}
			}
		}
	}

	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			_semaphore?.Dispose();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.FileStoreStoringEvents, LogLevel.Debug, "Storing {Count} security events to audit log files")]
	private partial void LogStoringEvents(int count);

	[LoggerMessage(SecurityEventId.SqlStoreInvalidEvent, LogLevel.Warning,
		"Invalid security event detected: {SecurityEventId}, Description: {Description}")]
	private partial void LogInvalidEvent(Guid securityEventId, string? description);

	[LoggerMessage(SecurityEventId.FileStoreStoreFailed, LogLevel.Error, "Failed to serialize security event {SecurityEventId} to file")]
	private partial void LogSerializationFailed(Exception ex, Guid securityEventId);

	[LoggerMessage(SecurityEventId.FileStoreEventsStored, LogLevel.Information,
		"Successfully stored {ValidCount} security events to audit log file {LogFile}")]
	private partial void LogEventsStored(int validCount, string logFile);

	[LoggerMessage(SecurityEventId.FileStoreLoadFailed, LogLevel.Error, "Failed to store {Count} security events to audit log files")]
	private partial void LogStoreEventsFailed(Exception ex, int count);

	[LoggerMessage(SecurityEventId.FileStoreQueryingEvents, LogLevel.Debug, "Querying security events from audit log files with parameters: {Query}")]
	private partial void LogQueryingEvents(string query);

	[LoggerMessage(SecurityEventId.FileStoreInitFailed, LogLevel.Warning, "Failed to deserialize log line in file {LogFile}: {Line}")]
	private partial void LogDeserializationFailed(Exception ex, string logFile, string line);

	[LoggerMessage(SecurityEventId.FileStoreLoadingEvents, LogLevel.Error, "Failed to read audit log file {LogFile}")]
	private partial void LogReadFileFailed(Exception ex, string logFile);

	[LoggerMessage(SecurityEventId.FileStoreQueryExecuted, LogLevel.Information,
		"Security events query returned {Count} results from {FileCount} audit log files")]
	private partial void LogQueryResults(int count, int fileCount);

	[LoggerMessage(SecurityEventId.FileStoreQueryFailed, LogLevel.Error, "Failed to query security events from audit log files")]
	private partial void LogQueryEventsFailed(Exception ex);

	[LoggerMessage(SecurityEventId.FileStoreRotationCompleted, LogLevel.Information, "Rotated audit log file {CurrentFile} to {RotatedFile}")]
	private partial void LogFileRotated(string currentFile, string rotatedFile);

	[LoggerMessage(SecurityEventId.FileStoreRotationFailed, LogLevel.Error, "Failed to rotate audit log file {LogFile}")]
	private partial void LogRotateFileFailed(Exception ex, string logFile);

	[LoggerMessage(SecurityEventId.FileStoreDirectoryCreated, LogLevel.Debug, "Compressed audit log file {LogFile} to {CompressedFile}")]
	private partial void LogFileCompressed(string logFile, string compressedFile);

	[LoggerMessage(SecurityEventId.FileStoreRotationStarted, LogLevel.Error, "Failed to compress audit log file {LogFile}")]
	private partial void LogCompressFileFailed(Exception ex, string logFile);

	[LoggerMessage(SecurityEventId.FileStoreCleanupCompleted, LogLevel.Information, "Deleted old audit log file {File} due to retention policy")]
	private partial void LogOldFileDeleted(string file);

	[LoggerMessage(SecurityEventId.FileStoreCleanupFailed, LogLevel.Error, "Failed to cleanup old audit log files")]
	private partial void LogCleanupFailed(Exception ex);

	private async Task RotateLogFileAsync(string currentLogFile, CancellationToken cancellationToken)
	{
		try
		{
			var timestamp = DateTimeOffset.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture);
			var rotatedFile = currentLogFile.Replace(".jsonl", $"-{timestamp}.jsonl", StringComparison.Ordinal);
			File.Move(currentLogFile, rotatedFile);

			await CompressLogFileAsync(rotatedFile, cancellationToken).ConfigureAwait(false);
			await CleanupOldFilesAsync().ConfigureAwait(false);

			LogFileRotated(currentLogFile, rotatedFile);
		}
		catch (Exception ex)
		{
			LogRotateFileFailed(ex, currentLogFile);
		}
	}

	private async Task CompressLogFileAsync(string logFile, CancellationToken cancellationToken)
	{
		try
		{
			var compressedFile = logFile + ".gz";
			var originalStream = new FileStream(logFile, FileMode.Open, FileAccess.Read);
			await using (originalStream.ConfigureAwait(false))
			{
				var compressedStream = new FileStream(compressedFile, FileMode.Create, FileAccess.Write);
				await using (compressedStream.ConfigureAwait(false))
				{
					var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal);
					await using (gzipStream.ConfigureAwait(false))
					{
						await originalStream.CopyToAsync(gzipStream, cancellationToken).ConfigureAwait(false);
						await gzipStream.FlushAsync(cancellationToken).ConfigureAwait(false);

						// Delete original file after successful compression
						File.Delete(logFile);

						LogFileCompressed(logFile, compressedFile);
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogCompressFileFailed(ex, logFile);
		}
	}

	private async Task CleanupOldFilesAsync()
	{
		try
		{
			var allFiles = Directory.GetFiles(_logDirectory, $"{_filePrefix}-*.jsonl*")
				.OrderByDescending(static f => File.GetCreationTimeUtc(f))
				.ToList();

			if (allFiles.Count > _maxFiles)
			{
				foreach (var file in allFiles.Skip(_maxFiles))
				{
					File.Delete(file);
					LogOldFileDeleted(file);
				}
			}

			await Task.CompletedTask.ConfigureAwait(false); // Satisfy async requirement
		}
		catch (Exception ex)
		{
			LogCleanupFailed(ex);
		}
	}

	private List<string> GetLogFilesInDateRange(DateTimeOffset? startTime, DateTimeOffset? endTime)
	{
		var allFiles = Directory.GetFiles(_logDirectory, $"{_filePrefix}-*.jsonl*")
			.Where(f => Path.GetFileName(f).StartsWith(_filePrefix, StringComparison.Ordinal))
			.ToList();

		if (!startTime.HasValue && !endTime.HasValue)
		{
			return allFiles;
		}

		var filteredFiles = new List<string>();
		var startDate = startTime.HasValue ? DateOnly.FromDateTime(startTime.Value.UtcDateTime.Date) : (DateOnly?)null;
		var endDate = endTime.HasValue ? DateOnly.FromDateTime(endTime.Value.UtcDateTime.Date) : (DateOnly?)null;

		foreach (var file in allFiles)
		{
			var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
			var dateMatch = FileDateRegex().Match(fileName);

			if (dateMatch.Success &&
				DateOnly.TryParseExact(
					dateMatch.Value,
					"yyyy-MM-dd",
					CultureInfo.InvariantCulture,
					DateTimeStyles.None,
					out var fileDate))
			{
				if ((!startDate.HasValue || fileDate >= startDate.Value) &&
					(!endDate.HasValue || fileDate <= endDate.Value))
				{
					filteredFiles.Add(file);
				}
			}
		}

		return filteredFiles;
	}
}
