// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Configures the partition a scoped distributed cache writes into.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Scope"/> is an opaque string. This package attaches no meaning to it and does not know where
/// it comes from — a host supplies a value that distinguishes one deployed application from another sharing
/// the same cache server.
/// </para>
/// </remarks>
public sealed class CacheKeyScopeOptions
{
	/// <summary>
	/// Gets or sets the partition every key written through the scoped cache is placed under.
	/// </summary>
	/// <value>
	/// A non-empty, non-whitespace string. There is deliberately no default: an empty scope would place two
	/// applications' entries in one keyspace, which is the condition the scoped cache exists to prevent, and
	/// a default would make that the outcome of forgetting to configure it.
	/// </value>
	public string Scope { get; set; } = string.Empty;
}

/// <summary>
/// Validates <see cref="CacheKeyScopeOptions"/> at startup.
/// </summary>
internal sealed class CacheKeyScopeOptionsValidator : IValidateOptions<CacheKeyScopeOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CacheKeyScopeOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return string.IsNullOrWhiteSpace(options.Scope)
			? ValidateOptionsResult.Fail(
				$"{nameof(CacheKeyScopeOptions)}.{nameof(CacheKeyScopeOptions.Scope)} must be a non-empty "
				+ "value that distinguishes this application from any other sharing the same cache server. "
				+ "It is left unset rather than defaulted because an empty scope silently merges those "
				+ "applications' keyspaces.")
			: ValidateOptionsResult.Success;
	}
}
