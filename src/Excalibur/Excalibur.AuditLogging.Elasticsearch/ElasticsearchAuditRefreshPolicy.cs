// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.AuditLogging.Elasticsearch;

/// <summary>
/// The refresh policy this exporter asks the search cluster to apply to its bulk write.
/// </summary>
/// <remarks>
/// <para>
/// <b>An enumeration rather than a string, because this value reaches a URL.</b> It was a shipped
/// <c>string</c> that was interpolated directly into the <c>_bulk</c> request as <c>?refresh=&lt;value&gt;</c>
/// with nothing constraining it, so a typo, a differently-cased spelling, or any arbitrary text was sent
/// to the server as a query-parameter value. The type now makes an unrecognised policy inexpressible
/// rather than merely unlikely, and the mapping to the wire value happens in exactly one place.
/// </para>
/// <para>
/// <b>The audience is what makes this worth a breaking change.</b> This is an AUDIT exporter: a silently
/// wrong refresh setting means audit records may not be searchable at the moment an auditor looks for
/// them, which is the one time their absence is expensive.
/// </para>
/// <para>
/// <b>Declared here rather than shared with the sibling search packages, deliberately.</b> The obvious
/// reuse would pull this lightweight HTTP-only exporter into a package carrying a full vendor client and
/// its transitive cloud SDKs, which is a far worse trade than a three-member enumeration appearing twice.
/// </para>
/// </remarks>
public enum ElasticsearchAuditRefreshPolicy
{
	/// <summary>Do not refresh. The fastest option, and the default: an audit write is not a read-your-writes path.</summary>
	None = 0,

	/// <summary>Wait for the next scheduled refresh before the request returns, so the record is searchable when it does.</summary>
	WaitFor = 1,

	/// <summary>Force a refresh immediately. Correct but expensive; it costs indexing throughput on every write.</summary>
	Immediate = 2,
}
