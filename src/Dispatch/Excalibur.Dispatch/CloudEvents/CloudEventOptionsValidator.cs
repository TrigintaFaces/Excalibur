// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Validates <see cref="CloudEventOptions"/> at startup so a misconfigured CloudEvents pipeline fails fast
/// (via <c>ValidateOnStart()</c>) rather than throwing on the first message.
/// </summary>
/// <remarks>
/// Without a registered validator, <c>AddOptions&lt;CloudEventOptions&gt;().ValidateOnStart()</c> is a no-op —
/// there is nothing to run. This closes that gap for the core options; per-transport option validators are
/// registered by their own transport extensions.
/// </remarks>
internal sealed class CloudEventOptionsValidator : IValidateOptions<CloudEventOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, CloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("CloudEventOptions must not be null.");
		}

		var failures = new List<string>();

		if (options.DefaultSource is null)
		{
			failures.Add($"{nameof(CloudEventOptions.DefaultSource)} is required.");
		}
		else if (!options.DefaultSource.IsAbsoluteUri)
		{
			failures.Add(
				$"{nameof(CloudEventOptions.DefaultSource)} must be an absolute URI (the CloudEvents 'source' " +
				$"attribute must be an absolute reference); got '{options.DefaultSource.OriginalString}'.");
		}

		if (string.IsNullOrWhiteSpace(options.DispatchExtensionPrefix))
		{
			failures.Add($"{nameof(CloudEventOptions.DispatchExtensionPrefix)} must not be null or whitespace.");
		}

		if (!Enum.IsDefined(options.Mode))
		{
			failures.Add($"{nameof(CloudEventOptions.Mode)} '{options.Mode}' is not a defined CloudEventMode.");
		}

		if (!Enum.IsDefined(options.DefaultMode))
		{
			failures.Add($"{nameof(CloudEventOptions.DefaultMode)} '{options.DefaultMode}' is not a defined CloudEventMode.");
		}

		if (options.Compression.CompressionThreshold < 0)
		{
			failures.Add(
				$"{nameof(CloudEventOptions.Compression)}.{nameof(CloudEventCompressionOptions.CompressionThreshold)} " +
				$"must be non-negative; got {options.Compression.CompressionThreshold}.");
		}

		// Fail fast at startup rather than on the first message: schema-payload validation is not yet
		// implemented (no payload-validation provider exists in any configuration), so enabling
		// ValidateSchema would reject every incoming CloudEvent at runtime. Keep it opt-out (default false,
		// the Microsoft OutputCache/HybridCache convention — validation gates are opt-in) until the
		// schema-registry validation feature ships.
		if (options.Schema.ValidateSchema)
		{
			failures.Add(
				$"{nameof(CloudEventOptions.Schema)}.{nameof(CloudEventSchemaOptions.ValidateSchema)} is enabled, but " +
				"CloudEvent schema-payload validation is not yet supported (no validation provider is available). " +
				"Leave it disabled until the schema-registry validation feature ships.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
