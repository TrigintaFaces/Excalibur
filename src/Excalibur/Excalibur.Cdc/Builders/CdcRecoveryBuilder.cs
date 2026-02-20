// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Internal implementation of the CDC recovery builder.
/// </summary>
internal sealed class CdcRecoveryBuilder : ICdcRecoveryBuilder
{
	private readonly CdcOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRecoveryBuilder"/> class.
	/// </summary>
	/// <param name="options">The CDC options to configure.</param>
	public CdcRecoveryBuilder(CdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public ICdcRecoveryBuilder Strategy(StalePositionRecoveryStrategy strategy)
	{
		_options.RecoveryStrategy = strategy;
		return this;
	}

	/// <inheritdoc/>
	public ICdcRecoveryBuilder MaxAttempts(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		_options.MaxRecoveryAttempts = count;
		return this;
	}

	/// <inheritdoc/>
	public ICdcRecoveryBuilder AttemptDelay(TimeSpan delay)
	{
		if (delay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay must be non-negative.");
		}

		_options.RecoveryAttemptDelay = delay;
		return this;
	}

	/// <inheritdoc/>
	public ICdcRecoveryBuilder OnPositionReset(CdcPositionResetHandler handler)
	{
		ArgumentNullException.ThrowIfNull(handler);
		_options.OnPositionReset = handler;
		return this;
	}

	/// <inheritdoc/>
	public ICdcRecoveryBuilder EnableStructuredLogging(bool enable = true)
	{
		_options.EnableStructuredLogging = enable;
		return this;
	}
}
