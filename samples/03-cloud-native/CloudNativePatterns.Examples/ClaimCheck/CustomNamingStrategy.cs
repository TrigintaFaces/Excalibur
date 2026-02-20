// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Custom naming strategy example.
/// </summary>
public class CustomNamingStrategy : IClaimCheckNamingStrategy
{
	/// <inheritdoc />
	public string GenerateId(ClaimCheckMetadata? metadata = null)
	{
		// Generate timestamp-based ID with optional metadata
		var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
		var messageType = metadata?.MessageType?.ToUpperInvariant() ?? "MSG";
		return $"{messageType}-{timestamp}-{Guid.NewGuid():N}";
	}

	/// <inheritdoc />
	public string GenerateStoragePath(string claimCheckId, ClaimCheckMetadata? metadata = null)
	{
		// Custom hierarchical naming: messageType/year/month/day/hour/claimId
		var now = DateTime.UtcNow;
		var messageType = metadata?.MessageType?.ToUpperInvariant() ?? "UNKNOWN";

		return $"{messageType}/{now:yyyy}/{now:MM}/{now:dd}/{now:HH}/{claimCheckId}";
	}

	public string GetContainerName(ClaimCheckMetadata metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		// Could use different containers per message type
		return metadata.MessageType?.ToUpperInvariant() ?? "DEFAULT-CLAIMS";
	}
}
