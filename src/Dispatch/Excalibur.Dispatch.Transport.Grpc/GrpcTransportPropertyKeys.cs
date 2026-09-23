// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// The transport-message property keys the gRPC adapter writes and the gRPC sender reads.
/// </summary>
/// <remarks>
/// <para>
/// These exist because the two halves of this transport agree on a string, and a shared literal is a
/// contract whether or not anyone writes it down. Leaving it undeclared is what let the adapter put the
/// caller's destination in one place while the sender looked for it in another: both files were
/// self-consistent, nothing failed to compile, and the value was simply dropped on the wire.
/// </para>
/// <para>
/// Note that these are NOT the telemetry tags in <c>TransportTelemetryConstants.Tags</c>, which carry a
/// different prefix and exist to label spans and metrics. A routing value and a span tag that happen to
/// describe the same thing are still two contracts, and collapsing them would make a change to either one
/// silently alter the other.
/// </para>
/// </remarks>
internal static class GrpcTransportPropertyKeys
{
	/// <summary>
	/// The caller's explicit destination, carried from the adapter to the outbound gRPC request.
	/// </summary>
	public const string Destination = "dispatch.destination";
}
