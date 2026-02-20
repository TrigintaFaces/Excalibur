// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Represents a message captured by the dead-letter queue.
/// </summary>
public sealed record TransportTestDeadLetterMessage(
	TransportTestMessage Message,
	string Reason,
	string? Details,
	DateTimeOffset DeadLetteredAtUtc,
	IReadOnlyDictionary<string, string> TransportMetadata);
