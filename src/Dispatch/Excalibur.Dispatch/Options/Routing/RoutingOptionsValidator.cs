// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Routing;

/// <summary>
/// Validates <see cref="RoutingOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Both properties are bindable from a consumer's configuration, so a whitespace-only value
/// (e.g. an empty environment variable substituted into an otherwise-set key) can reach here.
/// Failing at startup puts the error next to its cause instead of surfacing as a routing
/// failure on the first dispatched message.
/// </remarks>
internal sealed class RoutingOptionsValidator : IValidateOptions<RoutingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RoutingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.RoutingPolicyPath is { Length: > 0 } path && string.IsNullOrWhiteSpace(path))
		{
			failures.Add($"{nameof(RoutingOptions.RoutingPolicyPath)} must not be whitespace-only when set.");
		}

		if (options.DefaultRemoteBusName is { Length: > 0 } busName && string.IsNullOrWhiteSpace(busName))
		{
			failures.Add($"{nameof(RoutingOptions.DefaultRemoteBusName)} must not be whitespace-only when set.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
