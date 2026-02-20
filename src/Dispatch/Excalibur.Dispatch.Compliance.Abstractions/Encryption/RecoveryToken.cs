// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents a recovery token for key escrow recovery using Shamir's Secret Sharing.
/// </summary>
/// <remarks>
/// <para>
/// Recovery tokens are generated using Shamir's Secret Sharing scheme, which allows
/// a secret to be divided into multiple shares. A threshold number of shares (e.g., 3 of 5)
/// must be combined to reconstruct the original secret.
/// </para>
/// <para>
/// Each token represents one share and should be distributed to a different custodian.
/// The <see cref="ShareIndex"/> identifies which share this token represents.
/// </para>
/// </remarks>
public sealed record RecoveryToken
{
	private static readonly CompositeFormat ThresholdNotMetFormat =
			CompositeFormat.Parse(Resources.RecoveryToken_ThresholdNotMet);

	private static readonly CompositeFormat ExpiredTokensFormat =
			CompositeFormat.Parse(Resources.RecoveryToken_ExpiredTokens);

	/// <summary>
	/// Gets the unique identifier of this token.
	/// </summary>
	public required string TokenId { get; init; }

	/// <summary>
	/// Gets the key identifier this token can recover.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the escrow identifier this token belongs to.
	/// </summary>
	public required string EscrowId { get; init; }

	/// <summary>
	/// Gets the share index in the Shamir's Secret Sharing scheme (1-based).
	/// </summary>
	public required int ShareIndex { get; init; }

	/// <summary>
	/// Gets the share data (the actual share value from Shamir's algorithm).
	/// </summary>
	public required byte[] ShareData { get; init; }

	/// <summary>
	/// Gets the total number of shares generated.
	/// </summary>
	public required int TotalShares { get; init; }

	/// <summary>
	/// Gets the threshold number of shares required for recovery.
	/// </summary>
	public required int Threshold { get; init; }

	/// <summary>
	/// Gets the timestamp when this token was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when this token expires.
	/// </summary>
	public required DateTimeOffset ExpiresAt { get; init; }

	/// <summary>
	/// Gets a value indicating whether this token has expired.
	/// </summary>
	public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

	/// <summary>
	/// Gets the optional custodian identifier this token is assigned to.
	/// </summary>
	public string? CustodianId { get; init; }

	/// <summary>
	/// Combines multiple recovery tokens to reconstruct a complete recovery credential.
	/// </summary>
	/// <param name="tokens">The tokens to combine (must meet threshold).</param>
	/// <returns>A combined token that can be used for recovery.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when tokens are from different escrows or threshold is not met.
	/// </exception>
	public static RecoveryToken Combine(IEnumerable<RecoveryToken> tokens)
	{
		ArgumentNullException.ThrowIfNull(tokens);

		var tokenList = tokens.ToList();

		if (tokenList.Count == 0)
		{
			throw new ArgumentException(Resources.RecoveryToken_AtLeastOneTokenRequired, nameof(tokens));
		}

		var first = tokenList[0];

		// Validate all tokens are from the same escrow
		if (!tokenList.All(t => t.EscrowId == first.EscrowId && t.KeyId == first.KeyId))
		{
			throw new ArgumentException(Resources.RecoveryToken_SameEscrowRequired, nameof(tokens));
		}

		// Validate threshold is met
		if (tokenList.Count < first.Threshold)
		{
			throw new ArgumentException(
					string.Format(
							CultureInfo.CurrentCulture,
							ThresholdNotMetFormat,
							first.Threshold,
							tokenList.Count),
					nameof(tokens));
		}

		// Validate no duplicates
		var uniqueIndices = tokenList.Select(t => t.ShareIndex).Distinct().Count();
		if (uniqueIndices != tokenList.Count)
		{
			throw new ArgumentException(Resources.RecoveryToken_DuplicateShareIndices, nameof(tokens));
		}

		// Check for expired tokens
		var expired = tokenList.Where(t => t.IsExpired).ToList();
		if (expired.Count > 0)
		{
			throw new ArgumentException(
					string.Format(
							CultureInfo.CurrentCulture,
							ExpiredTokensFormat,
							string.Join(", ", expired.Select(t => t.TokenId))),
					nameof(tokens));
		}

		// Create a combined token with all share data concatenated
		// The actual Shamir reconstruction happens in the escrow service
		var combinedShareData = tokenList
			.OrderBy(t => t.ShareIndex)
			.SelectMany(t => BitConverter.GetBytes(t.ShareIndex).Concat(t.ShareData))
			.ToArray();

		return new RecoveryToken
		{
			TokenId = $"combined-{Guid.NewGuid():N}",
			KeyId = first.KeyId,
			EscrowId = first.EscrowId,
			ShareIndex = 0, // 0 indicates a combined token
			ShareData = combinedShareData,
			TotalShares = first.TotalShares,
			Threshold = first.Threshold,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = tokenList.Min(t => t.ExpiresAt)
		};
	}
}
