// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Reporting;

/// <summary>
/// Formats an <see cref="EntitlementSnapshot"/> into a byte representation.
/// </summary>
/// <remarks>
/// <para>
/// The framework provides a built-in JSON formatter. Consumers can implement this interface
/// for CSV, PDF, or other output formats.
/// </para>
/// </remarks>
public interface IReportFormatter
{
	/// <summary>
	/// Gets the MIME content type produced by this formatter (e.g., <c>"application/json"</c>).
	/// </summary>
	/// <value>The content type string.</value>
	string ContentType { get; }

	/// <summary>
	/// Formats the snapshot into a byte array.
	/// </summary>
	/// <param name="snapshot">The entitlement snapshot to format.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The formatted report as a byte array.</returns>
	Task<byte[]> FormatAsync(EntitlementSnapshot snapshot, CancellationToken cancellationToken);
}
