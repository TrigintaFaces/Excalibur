// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Outbox;

/// <summary>Immutable acquisition evidence captured before any consumer callback.</summary>
internal readonly record struct OutboxClaimEvidence
{
	internal OutboxClaimEvidence(string messageId, string claimIdentity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);
		MessageId = messageId;
		ClaimIdentity = claimIdentity;
	}

	internal string MessageId { get; }
	internal string ClaimIdentity { get; }
}
