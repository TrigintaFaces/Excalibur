// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Signals that a response the cluster returned was not marked successful, distinct from an exception the
/// client itself threw.
/// </summary>
/// <remarks>
/// Its own type on purpose. Thrown inside the Polly pipeline delegate, it travels the same path as a
/// thrown failure so the retry and the breaker both see it, and it carries the response's HTTP status
/// code (when known) so <see cref="ElasticsearchResiliencePipeline.IsTransient" /> can judge it by the
/// same rule it applies to a <see cref="Elastic.Transport.TransportException" /> reporting the same
/// status -- rather than treating it as permanently non-retriable just because the client returned the
/// failure instead of throwing it.
/// </remarks>
internal sealed class ElasticsearchInvalidResponseException(string operationType, int? httpStatusCode)
	: InvalidOperationException($"{operationType} operation returned invalid response")
{
	/// <summary>
	/// Gets the HTTP status code the response reported, when known.
	/// </summary>
	public int? HttpStatusCode { get; } = httpStatusCode;
}
