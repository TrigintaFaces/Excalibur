// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Policy.Opa;

/// <summary>
/// Parses OPA JSON responses into <see cref="AuthorizationDecision"/> values.
/// </summary>
internal static class OpaResponseParser
{
	private static readonly JsonDocumentOptions s_jsonOptions = new()
	{
		AllowTrailingCommas = true,
	};

	/// <summary>
	/// Parses the OPA response body and returns an authorization decision.
	/// </summary>
	/// <remarks>
	/// Expected OPA response format: <c>{ "result": true|false }</c>.
	/// If the response is malformed or the <c>result</c> property is missing,
	/// returns <see cref="AuthorizationEffect.Deny"/> with a reason.
	/// </remarks>
	internal static AuthorizationDecision Parse(ReadOnlyMemory<byte> responseBody)
	{
		try
		{
			using var doc = JsonDocument.Parse(responseBody, s_jsonOptions);

			if (doc.RootElement.TryGetProperty("result", out var resultElement))
			{
				if (resultElement.ValueKind == JsonValueKind.True)
				{
					return new AuthorizationDecision(AuthorizationEffect.Permit);
				}

				if (resultElement.ValueKind == JsonValueKind.False)
				{
					return new AuthorizationDecision(AuthorizationEffect.Deny, "OPA policy denied the request.");
				}

				// result is present but not a boolean (could be an object with nested result)
				if (resultElement.ValueKind == JsonValueKind.Object &&
					resultElement.TryGetProperty("allow", out var allowElement))
				{
					return allowElement.ValueKind == JsonValueKind.True
						? new AuthorizationDecision(AuthorizationEffect.Permit)
						: new AuthorizationDecision(AuthorizationEffect.Deny, "OPA policy denied the request.");
				}
			}

			return new AuthorizationDecision(AuthorizationEffect.Deny, "OPA response missing 'result' property.");
		}
		catch (JsonException)
		{
			return new AuthorizationDecision(AuthorizationEffect.Deny, "OPA response is not valid JSON.");
		}
	}
}
