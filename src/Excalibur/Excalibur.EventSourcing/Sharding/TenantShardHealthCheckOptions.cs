// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Sharding;

/// <summary>Controls bounded fan-out and the registered tenant-shard readiness timeout.</summary>
public sealed class TenantShardHealthCheckOptions
{
	/// <summary>Gets or sets the maximum concurrent physical-shard reads. Defaults to four.</summary>
	public int MaxConcurrentProbes { get; set; } = 4;

	/// <summary>Gets or sets the finite health-check timeout. Defaults to thirty seconds.</summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	internal void Validate()
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentProbes);
		if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMilliseconds(uint.MaxValue - 1d))
		{
			throw new ArgumentOutOfRangeException(nameof(Timeout), "A positive finite timer-supported timeout is required.");
		}
	}
}
