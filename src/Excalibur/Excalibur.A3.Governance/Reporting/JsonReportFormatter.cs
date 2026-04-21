// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.A3.Governance.NonHumanIdentity;
using Excalibur.A3.Governance.Reporting;

namespace Excalibur.A3.Governance;

/// <summary>
/// Formats entitlement snapshots as JSON using source-generated serialization (AOT-safe).
/// </summary>
internal sealed class JsonReportFormatter : IReportFormatter
{
	/// <inheritdoc />
	public string ContentType => "application/json";

	/// <inheritdoc />
	public Task<byte[]> FormatAsync(EntitlementSnapshot snapshot, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, ReportJsonContext.Default.EntitlementSnapshot);
		return Task.FromResult(bytes);
	}
}

[JsonSerializable(typeof(EntitlementSnapshot))]
[JsonSerializable(typeof(EntitlementEntry))]
[JsonSerializable(typeof(EntitlementReviewStatus))]
[JsonSerializable(typeof(EntitlementReportType))]
[JsonSerializable(typeof(PrincipalType))]
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = true)]
internal sealed partial class ReportJsonContext : JsonSerializerContext;
