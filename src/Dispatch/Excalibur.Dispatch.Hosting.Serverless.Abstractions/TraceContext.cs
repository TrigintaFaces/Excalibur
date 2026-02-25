// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Distributed tracing context for serverless functions.
/// </summary>
public sealed record TraceContext
{
	/// <summary>
	/// Gets the trace ID.
	/// </summary>
	/// <value>The trace ID.</value>
	public string TraceId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the span ID.
	/// </summary>
	/// <value>The span ID.</value>
	public string SpanId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the parent span ID.
	/// </summary>
	/// <value>The parent span ID.</value>
	public string? ParentSpanId { get; init; }

	/// <summary>
	/// Gets trace flags.
	/// </summary>
	/// <value>The trace flags.</value>
	public string? TraceFlags { get; init; }

	/// <summary>
	/// Gets additional trace state.
	/// </summary>
	/// <value>The additional trace state.</value>
	public string? TraceState { get; init; }

	/// <summary>
	/// Gets the W3C traceparent header value.
	/// </summary>
	/// <value>The W3C traceparent header value.</value>
	public string? TraceParent { get; init; }
}
