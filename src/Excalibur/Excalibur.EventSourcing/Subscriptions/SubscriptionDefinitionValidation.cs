// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>One validation and comparison rule for subscription rosters across providers.</summary>
internal static class SubscriptionDefinitionValidation
{
    internal static void ValidateName(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            throw new ArgumentException($"Names must be at most {maximumLength} characters and cannot start or end with whitespace.", parameterName);
        }
    }

	internal static void ValidateBinding(string subscriptionName, string projectionKey, Guid bindingId)
	{
		ValidateName(subscriptionName, nameof(subscriptionName), MaxSubscriptionNameLength);
		ValidateName(projectionKey, nameof(projectionKey), MaxProjectionKeyLength);
		if (bindingId == Guid.Empty) { throw new ArgumentException("A stored binding identity is required.", nameof(bindingId)); }
	}

	internal static void ValidateAdvance(string subscriptionName, string projectionKey, Guid bindingId, long? expectedPosition, long newPosition)
	{
		ValidateBinding(subscriptionName, projectionKey, bindingId);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		if (expectedPosition is { } prior)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(prior, nameof(expectedPosition));
			ArgumentOutOfRangeException.ThrowIfLessThan(newPosition, prior);
		}
	}

	private const int MaxSubscriptionNameLength = 255;
	private const int MaxProjectionKeyLength = 512;

	/// <summary>
	/// The destination identity's length cap.
	/// </summary>
	/// <remarks>
	/// STATED SO A PROVIDER DOES NOT HAVE TO GUESS A COLUMN WIDTH. The contract previously bounded the
	/// subscription name and the projection key and said nothing about the destination, which left every
	/// relational provider picking its own width and discovering the limit as an insert failure. An index,
	/// table or container identity is far shorter than this; the cap exists to make the column width
	/// derivable from the contract rather than from each provider's judgement.
	/// </remarks>
	private const int MaxDestinationLength = 512;

	private const int FingerprintLength = 64;

	/// <summary>
	/// Validates a roster binding request before any storage access.
	/// </summary>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="members">The members this caller hosts.</param>
	internal static void Validate(string subscriptionName, IReadOnlyList<SubscriptionMember> members)
	{
		ValidateName(subscriptionName, nameof(subscriptionName), MaxSubscriptionNameLength);
		ArgumentNullException.ThrowIfNull(members);

		if (subscriptionName.Length > MaxSubscriptionNameLength)
		{
			throw new ArgumentException(
				$"A subscription name must not exceed {MaxSubscriptionNameLength} characters.",
				nameof(subscriptionName));
		}

		// Ordinal, matching how the key is compared in storage. A set that differs only by case holds two
		// members whose checkpoint keys also differ only by case, which is legal and deliberate -- what is
		// refused here is the same key twice, where the second occurrence would silently win or lose
		// depending on the provider's insert order.
		var seen = new HashSet<string>(StringComparer.Ordinal);

		foreach (var member in members)
		{
			ArgumentNullException.ThrowIfNull(member);
			ValidateMember(member);

			if (!seen.Add(member.ProjectionKey))
			{
				throw new ArgumentException(
					$"Projection key '{member.ProjectionKey}' appears more than once in the roster. "
					+ "Each projection binds exactly one member.",
					nameof(members));
			}
		}
	}

	/// <summary>Validates one member's key, fingerprint and destination.</summary>
	/// <param name="member">The member to validate.</param>
	internal static void ValidateMember(SubscriptionMember member)
	{
		ArgumentNullException.ThrowIfNull(member);
		ValidateName(member.ProjectionKey, nameof(member.ProjectionKey), MaxProjectionKeyLength);
		ArgumentNullException.ThrowIfNull(member.Destination);
        if (member.DestinationScope is { } scope) { ValidateName(scope, nameof(member.DestinationScope), 512); }

		if (member.ProjectionKey.Length > MaxProjectionKeyLength)
		{
			throw new ArgumentException(
				$"A projection key must not exceed {MaxProjectionKeyLength} characters.",
				nameof(member));
		}

		if (member.Destination.Length > MaxDestinationLength)
		{
			throw new ArgumentException(
				$"A destination identity must not exceed {MaxDestinationLength} characters.",
				nameof(member));
		}

		if (member.DefinitionHash is not { Length: FingerprintLength } hash
			|| hash.Any(static c => c is not ((>= '0' and <= '9') or (>= 'A' and <= 'F'))))
		{
			throw new ArgumentException(
				"A subscription definition must be an uppercase 64-character hexadecimal SHA-256 fingerprint.",
				nameof(member));
		}
	}

	/// <summary>
	/// Requires that a member already bound in the store carries the fingerprint this caller computed.
	/// </summary>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="requested">The member as this caller computed it.</param>
	/// <param name="stored">The member as the store holds it.</param>
	/// <remarks>
	/// THE MESSAGE NAMES THE DELTA, AND THAT IS THE POINT. The previous rule compared one fingerprint for
	/// the whole registration set, so a refusal could say only that "a different definition" was bound --
	/// leaving the consumer to bisect their own registration list to discover which projection, or which
	/// index name, had moved. Comparing per member, with the destination persisted beside the
	/// fingerprint, lets the refusal say exactly what changed.
	/// </remarks>
	internal static void RequireMemberMatch(
		string subscriptionName,
		SubscriptionMember requested,
		SubscriptionMember stored)
	{
		ArgumentNullException.ThrowIfNull(requested);
		ArgumentNullException.ThrowIfNull(stored);

		if (string.Equals(stored.DefinitionHash, requested.DefinitionHash, StringComparison.Ordinal)
			&& string.Equals(stored.Destination, requested.Destination, StringComparison.Ordinal)
            && string.Equals(stored.DestinationScope, requested.DestinationScope, StringComparison.Ordinal))
		{
			return;
		}

		var detail = string.Equals(stored.Destination, requested.Destination, StringComparison.Ordinal)
			? $"Its destination is unchanged ('{stored.Destination}'), so its scope, revision, assembly or "
				+ "type identity differs."
			: $"Its destination moved from '{stored.Destination}' to '{requested.Destination}'.";

		throw new InvalidOperationException(
			$"Projection '{requested.ProjectionKey}' is already bound to a different processing definition "
			+ $"under subscription '{subscriptionName}'. {detail} "
			+ "All cooperating processors must agree on a projection's scope, revision and destination. "
			+ "Restore that definition, or release this member and rebuild it deliberately -- releasing "
			+ "one member re-folds that projection alone and leaves its siblings' cursors untouched.");
	}
}
