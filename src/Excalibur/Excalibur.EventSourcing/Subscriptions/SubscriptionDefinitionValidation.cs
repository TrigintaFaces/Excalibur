// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>One validation and comparison rule for subscription definitions across providers.</summary>
internal static class SubscriptionDefinitionValidation
{
	internal static void Validate(string subscriptionName, string definitionHash)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
		ArgumentException.ThrowIfNullOrWhiteSpace(definitionHash);
		if (subscriptionName.Length > 255)
		{
			throw new ArgumentException("A subscription name must not exceed 255 characters.", nameof(subscriptionName));
		}

		if (definitionHash.Length != 64 || definitionHash.Any(static c =>
			c is not ((>= '0' and <= '9') or (>= 'A' and <= 'F'))))
		{
			throw new ArgumentException(
				"A subscription definition must be an uppercase 64-character hexadecimal SHA-256 fingerprint.",
				nameof(definitionHash));
		}
	}

	internal static void RequireMatch(string subscriptionName, string definitionHash, string? storedHash)
	{
		if (!string.Equals(storedHash, definitionHash, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Subscription '{subscriptionName}' is bound to a different processing definition. "
				+ "All cooperating processors must use the same projections, source/destination scope and revision. "
				+ "Restore that definition, or use a new subscription name and rebuild deliberately.");
		}
	}
}
