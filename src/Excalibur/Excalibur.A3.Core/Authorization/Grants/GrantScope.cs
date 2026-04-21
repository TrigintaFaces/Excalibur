// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents the scope of a grant, including tenant ID, grant type, and qualifier.
/// </summary>
/// <param name="TenantId"> The tenant ID associated with the scope. </param>
/// <param name="GrantType"> The type of the grant. </param>
/// <param name="Qualifier"> A qualifier providing additional context for the grant type. </param>
/// <exception cref="ArgumentNullException"> Thrown if any parameter is null or empty. </exception>
public record GrantScope(string TenantId, string GrantType, string Qualifier)
{
	/// <summary>
	/// Gets or sets the tenant ID associated with the scope.
	/// </summary>
	/// <value>The tenant ID.</value>
	public string TenantId { get; init; } = !string.IsNullOrEmpty(TenantId) ? TenantId : throw new ArgumentNullException(nameof(TenantId));

	/// <summary>
	/// Gets or sets the type of the grant.
	/// </summary>
	/// <value>The type of the grant.</value>
	public string GrantType { get; init; } = !string.IsNullOrEmpty(GrantType) ? GrantType : throw new ArgumentNullException(nameof(GrantType));

	/// <summary>
	/// Gets or sets the qualifier providing additional context for the grant type.
	/// </summary>
	/// <value>The qualifier for the grant type.</value>
	public string Qualifier { get; init; } = !string.IsNullOrEmpty(Qualifier) ? Qualifier : throw new ArgumentNullException(nameof(Qualifier));

	/// <summary>
	/// Gets a value indicating whether this scope contains any wildcard segments.
	/// A scope is a wildcard if any segment is <c>*</c> or the qualifier ends with <c>.*</c> or <c>/*</c>.
	/// </summary>
	/// <value><see langword="true"/> if the scope contains wildcard segments; otherwise, <see langword="false"/>.</value>
	internal bool IsWildcard =>
		TenantId == "*" || GrantType == "*" ||
		Qualifier == "*" ||
		Qualifier.EndsWith(".*", StringComparison.Ordinal) ||
		Qualifier.EndsWith("/*", StringComparison.Ordinal);

	/// <summary>
	/// Gets the specificity score for wildcard precedence ordering.
	/// Higher score = more specific = higher priority. Exact matches score highest.
	/// Non-wildcard segments contribute 1000 each; suffix prefix length is used for tiebreaking.
	/// </summary>
	/// <value>The specificity score.</value>
	internal int SpecificityScore
	{
		get
		{
			var score = 0;
			if (TenantId != "*")
			{
				score += 1000;
			}

			if (GrantType != "*")
			{
				score += 1000;
			}

			if (Qualifier == "*")
			{
				return score;
			}

			if (Qualifier.EndsWith(".*", StringComparison.Ordinal) ||
				Qualifier.EndsWith("/*", StringComparison.Ordinal))
			{
				score += Qualifier.Length - 2; // prefix length for tiebreaking
				return score;
			}

			score += 1000; // exact qualifier match
			return score;
		}
	}

	/// <summary>
	/// Validates whether the specified scope string contains valid wildcard patterns.
	/// Wildcards must be a full <c>*</c> segment for TenantId/GrantType, or <c>*</c>, <c>{prefix}.*</c>,
	/// or <c>{prefix}/*</c> for the Qualifier. Patterns like <c>**</c>, <c>*partial</c>, or mid-qualifier
	/// wildcards are rejected.
	/// </summary>
	/// <param name="tenantId">The tenant ID segment.</param>
	/// <param name="grantType">The grant type segment.</param>
	/// <param name="qualifier">The qualifier segment.</param>
	/// <param name="error">When the method returns <see langword="false"/>, contains a description of the validation failure.</param>
	/// <returns><see langword="true"/> if the wildcard patterns are valid; otherwise, <see langword="false"/>.</returns>
	public static bool Validate(string tenantId, string grantType, string qualifier, out string? error)
	{
		error = null;

		// TenantId: must be either a concrete value or exactly "*"
		if (tenantId.Contains('*', StringComparison.Ordinal) && tenantId != "*")
		{
			error = "TenantId must be either a concrete value or exactly '*'.";
			return false;
		}

		// GrantType: must be either a concrete value or exactly "*"
		if (grantType.Contains('*', StringComparison.Ordinal) && grantType != "*")
		{
			error = "GrantType must be either a concrete value or exactly '*'.";
			return false;
		}

		// Qualifier: if it contains a wildcard, must be one of the valid patterns
		if (qualifier.Contains('*', StringComparison.Ordinal))
		{
			if (qualifier == "*")
			{
				return true;
			}

			// Must end with .* or /* and have a non-empty prefix
			if (qualifier.EndsWith(".*", StringComparison.Ordinal) && qualifier.Length > 2)
			{
				var prefix = qualifier.AsSpan(0, qualifier.Length - 2);
				if (!prefix.Contains('*'))
				{
					return true;
				}
			}

			if (qualifier.EndsWith("/*", StringComparison.Ordinal) && qualifier.Length > 2)
			{
				var prefix = qualifier.AsSpan(0, qualifier.Length - 2);
				if (!prefix.Contains('*'))
				{
					return true;
				}
			}

			error = "Qualifier wildcard must be '*', '{prefix}.*', or '{prefix}/*'. Patterns like '**', '*partial', or mid-qualifier wildcards are not allowed.";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Creates a <see cref="GrantScope" /> instance from a string in the format '[TenantId]:[GrantType]:[Qualifier]'.
	/// </summary>
	/// <param name="scope"> The scope string to parse. </param>
	/// <returns> A new <see cref="GrantScope" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="scope" /> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown if the format of <paramref name="scope" /> is invalid. </exception>
	public static GrantScope FromString(string scope)
	{
		ArgumentNullException.ThrowIfNull(scope);

		var parts = scope.Split(':', 3, StringSplitOptions.RemoveEmptyEntries);

		if (parts.Length != 3)
		{
			throw new ArgumentException("The scope is invalid. The expected format is '[TenantId]:[GrantType]:[Qualifier]'");
		}

		return new GrantScope(parts[0], parts[1], parts[2]);
	}

	/// <summary>
	/// Returns a string representation of the scope in the format '[TenantId]:[GrantType]:[Qualifier]'.
	/// </summary>
	/// <returns> A string representation of the scope. </returns>
	public override string ToString() => $"{TenantId}:{GrantType}:{Qualifier}";
}
