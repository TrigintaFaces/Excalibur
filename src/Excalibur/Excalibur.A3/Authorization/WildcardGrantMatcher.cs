// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Evaluates wildcard grant URNs against request URNs using segment matching
/// and qualifier suffix matching. Zero-allocation hot-path implementation.
/// </summary>
internal static class WildcardGrantMatcher
{
	/// <summary>
	/// Tests whether a wildcard grant scope matches a request scope.
	/// </summary>
	/// <param name="grantTenant">The grant's tenant ID segment.</param>
	/// <param name="grantType">The grant's grant type segment.</param>
	/// <param name="grantQualifier">The grant's qualifier segment.</param>
	/// <param name="requestTenant">The request's tenant ID.</param>
	/// <param name="requestType">The request's grant type.</param>
	/// <param name="requestQualifier">The request's qualifier.</param>
	/// <returns><see langword="true"/> if the grant matches the request; otherwise, <see langword="false"/>.</returns>
	internal static bool Matches(
		ReadOnlySpan<char> grantTenant,
		ReadOnlySpan<char> grantType,
		ReadOnlySpan<char> grantQualifier,
		ReadOnlySpan<char> requestTenant,
		ReadOnlySpan<char> requestType,
		ReadOnlySpan<char> requestQualifier)
	{
		// Segment matching: TenantId
		if (grantTenant is not "*" &&
			!grantTenant.Equals(requestTenant, StringComparison.Ordinal))
		{
			return false;
		}

		// Segment matching: GrantType
		if (grantType is not "*" &&
			!grantType.Equals(requestType, StringComparison.Ordinal))
		{
			return false;
		}

		// Qualifier matching: full wildcard
		if (grantQualifier is "*")
		{
			return true;
		}

		// Qualifier matching: suffix wildcard "prefix.*"
		if (grantQualifier.EndsWith(".*", StringComparison.Ordinal))
		{
			var prefix = grantQualifier[..^1]; // keep the dot: "Orders."
			return requestQualifier.StartsWith(prefix, StringComparison.Ordinal);
		}

		// Qualifier matching: path wildcard "prefix/*"
		if (grantQualifier.EndsWith("/*", StringComparison.Ordinal))
		{
			var prefix = grantQualifier[..^1]; // keep the slash: "orders/"
			return requestQualifier.StartsWith(prefix, StringComparison.Ordinal);
		}

		// Exact qualifier match
		return grantQualifier.Equals(requestQualifier, StringComparison.Ordinal);
	}
}
