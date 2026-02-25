// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Represents a received transport message and the associated receipt token.
/// </summary>
public sealed record TransportTestReceiveContext(
	TransportTestMessage Message,
	string ReceiptToken,
	int DeliveryAttempt,
	DateTimeOffset EnqueuedAtUtc,
	DateTimeOffset? LastDeliveredAtUtc,
	IReadOnlyDictionary<string, string> TransportMetadata);
