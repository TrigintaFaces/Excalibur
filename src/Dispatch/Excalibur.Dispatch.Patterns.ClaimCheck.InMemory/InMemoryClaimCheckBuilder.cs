// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Internal implementation of the in-memory claim check builder.
/// </summary>
internal sealed class InMemoryClaimCheckBuilder : IInMemoryClaimCheckBuilder
{
	private readonly ClaimCheckOptions _options;

	/// <summary>
	/// Gets a value indicating whether cleanup is enabled.
	/// </summary>
	internal bool CleanupEnabled { get; private set; } = true;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryClaimCheckBuilder"/> class.
	/// </summary>
	/// <param name="options">The claim check options to configure.</param>
	public InMemoryClaimCheckBuilder(ClaimCheckOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IInMemoryClaimCheckBuilder PayloadThreshold(long thresholdBytes)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdBytes);
		_options.PayloadThreshold = thresholdBytes;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryClaimCheckBuilder DefaultTtl(TimeSpan ttl)
	{
		if (ttl <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "TTL must be positive.");
		}

		_options.DefaultTtl = ttl;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryClaimCheckBuilder EnableCompression(bool enable = true)
	{
		_options.EnableCompression = enable;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryClaimCheckBuilder EnableCleanup(bool enable = true)
	{
		CleanupEnabled = enable;
		_options.EnableCleanup = enable;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryClaimCheckBuilder ValidateChecksum(bool enable = true)
	{
		_options.ValidateChecksum = enable;
		return this;
	}
}
