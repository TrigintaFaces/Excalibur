// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Represents a logical transport message used by the conformance test harness.
/// </summary>
public sealed record TransportTestMessage(string Id, string Body, IReadOnlyDictionary<string, string> Headers)
{
	/// <summary>
	///     Creates a deterministic message with optional overrides for identifier, body, and headers.
	/// </summary>
	public static TransportTestMessage Create(
		string? id = null,
		string? body = null,
		IReadOnlyDictionary<string, string>? headers = null)
	{
		var messageId = id ?? Guid.NewGuid().ToString("N");
		var payload = body ?? $"payload-{Guid.NewGuid():N}";
		var normalizedHeaders = headers is null
			? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);

		normalizedHeaders["transport-message-id"] = messageId;
		return new TransportTestMessage(messageId, payload, normalizedHeaders);
	}
}
