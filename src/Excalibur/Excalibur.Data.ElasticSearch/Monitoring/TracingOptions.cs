// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Configures distributed tracing integration for Elasticsearch operations.
/// </summary>
public sealed class TracingOptions
{
	/// <summary>
	/// Gets a value indicating whether tracing is enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether distributed tracing is active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to record operation details in spans.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to include operation metadata. Defaults to <c> true </c>. </value>
	public bool RecordOperationDetails { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to record request and response details in spans.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to include request/response data. Defaults to <c> false </c>. </value>
	public bool RecordRequestResponse { get; init; }

	/// <summary>
	/// Gets a value indicating whether to record resilience operation details in spans.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to trace retry attempts and circuit breaker state. Defaults to <c> true </c>. </value>
	public bool RecordResilienceDetails { get; init; } = true;

	/// <summary>
	/// Gets the activity source name for tracing.
	/// </summary>
	/// <value> A <see cref="string" /> representing the activity source identifier. Defaults to "Excalibur.Data.ElasticSearch". </value>
	public string ActivitySourceName { get; init; } = "Excalibur.Data.ElasticSearch";
}
