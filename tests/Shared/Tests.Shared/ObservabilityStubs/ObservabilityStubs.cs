// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.ObservabilityStubs;

/// <summary>Log capture interface stub for integration tests.</summary>
public interface ILogCapture
{
	/// <summary>Gets the captured log entries.</summary>
	IReadOnlyList<LogEntry> Entries { get; }

	/// <summary>Clears captured entries.</summary>
	void Clear();
}

/// <summary>Log entry stub.</summary>
public class LogEntry
{
	/// <summary>Gets or sets the log level.</summary>
	public string Level { get; set; } = string.Empty;

	/// <summary>Gets or sets the message.</summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>Gets or sets the timestamp.</summary>
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	/// <summary>Gets or sets the exception.</summary>
	public Exception? Exception { get; set; }
}

/// <summary>Trace exporter interface stub for integration tests.</summary>
public interface ITraceExporter
{
	/// <summary>Gets the exported spans.</summary>
	IReadOnlyList<SpanData> Spans { get; }

	/// <summary>Clears exported spans.</summary>
	void Clear();
}

/// <summary>Span data stub.</summary>
public class SpanData
{
	/// <summary>Gets or sets the trace ID.</summary>
	public string TraceId { get; set; } = string.Empty;

	/// <summary>Gets or sets the span ID.</summary>
	public string SpanId { get; set; } = string.Empty;

	/// <summary>Gets or sets the operation name.</summary>
	public string OperationName { get; set; } = string.Empty;

	/// <summary>Gets or sets the start time.</summary>
	public DateTime StartTime { get; set; } = DateTime.UtcNow;

	/// <summary>Gets or sets the duration.</summary>
	public TimeSpan Duration { get; set; }

	/// <summary>Gets or sets the tags.</summary>
	public Dictionary<string, object> Tags { get; set; } = new();
}
