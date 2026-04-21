// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Policy.Cedar;

/// <summary>
/// Parses Cedar JSON responses into <see cref="AuthorizationDecision"/> values.
/// </summary>
internal static class CedarResponseParser
{
	private static readonly JsonDocumentOptions s_jsonOptions = new()
	{
		AllowTrailingCommas = true,
	};

	/// <summary>
	/// Parses a local Cedar agent response.
	/// Expected format: <c>{ "decision": "Allow"|"Deny" }</c>.
	/// </summary>
	internal static AuthorizationDecision ParseLocal(ReadOnlyMemory<byte> responseBody)
	{
		return ParseDecisionProperty(responseBody, "decision");
	}

	/// <summary>
	/// Parses an Amazon Verified Permissions (AVP) response.
	/// Expected format: <c>{ "decision": "ALLOW"|"DENY" }</c>.
	/// </summary>
	internal static AuthorizationDecision ParseAvp(ReadOnlyMemory<byte> responseBody)
	{
		return ParseDecisionProperty(responseBody, "decision");
	}

	private static AuthorizationDecision ParseDecisionProperty(ReadOnlyMemory<byte> responseBody, string propertyName)
	{
		try
		{
			using var doc = JsonDocument.Parse(responseBody, s_jsonOptions);

			if (doc.RootElement.TryGetProperty(propertyName, out var decisionElement) &&
				decisionElement.ValueKind == JsonValueKind.String)
			{
				var decision = decisionElement.GetString();

				if (string.Equals(decision, "Allow", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(decision, "ALLOW", StringComparison.Ordinal))
				{
					return new AuthorizationDecision(AuthorizationEffect.Permit);
				}

				return new AuthorizationDecision(AuthorizationEffect.Deny, $"Cedar policy decision: {decision}");
			}

			return new AuthorizationDecision(AuthorizationEffect.Deny, $"Cedar response missing '{propertyName}' property.");
		}
		catch (JsonException)
		{
			return new AuthorizationDecision(AuthorizationEffect.Deny, "Cedar response is not valid JSON.");
		}
	}
}
