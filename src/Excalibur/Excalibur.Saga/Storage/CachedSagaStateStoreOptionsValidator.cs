// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Storage;

/// <summary>
/// Validates <see cref="CachedSagaStateStoreOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces <c>[Range(typeof(TimeSpan), ...)]</c> attributes with AOT-safe checks.
/// </summary>
internal sealed class CachedSagaStateStoreOptionsValidator : IValidateOptions<CachedSagaStateStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CachedSagaStateStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultCacheTtl < TimeSpan.FromSeconds(1) || options.DefaultCacheTtl > TimeSpan.FromDays(1))
		{
			failures.Add(
				$"{nameof(CachedSagaStateStoreOptions.DefaultCacheTtl)} must be between 00:00:01 and 24:00:00 (was {options.DefaultCacheTtl}).");
		}

		if (options.ActiveSagaCacheTtl < TimeSpan.FromSeconds(1) || options.ActiveSagaCacheTtl > TimeSpan.FromHours(1))
		{
			failures.Add(
				$"{nameof(CachedSagaStateStoreOptions.ActiveSagaCacheTtl)} must be between 00:00:01 and 01:00:00 (was {options.ActiveSagaCacheTtl}).");
		}

		if (options.CompletedSagaCacheTtl < TimeSpan.FromMinutes(1) || options.CompletedSagaCacheTtl > TimeSpan.FromDays(1))
		{
			failures.Add(
				$"{nameof(CachedSagaStateStoreOptions.CompletedSagaCacheTtl)} must be between 00:01:00 and 24:00:00 (was {options.CompletedSagaCacheTtl}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
