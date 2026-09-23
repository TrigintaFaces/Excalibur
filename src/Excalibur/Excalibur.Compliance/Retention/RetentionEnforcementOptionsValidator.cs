// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Retention;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="RetentionEnforcementOptions"/> at startup. Reflection-free (AOT-safe).</summary>
/// <remarks>
/// Enabled enforcement with no declared retention scope fails here, at startup, unless every registered
/// contributor is one that does not read the declared policies (the built-in outbox and inbox contributors,
/// which delete by their own age bound). Enforcement never hands an empty scope to a contributor that acts
/// on it, and never falls back to an ambient one.
/// </remarks>
internal sealed class RetentionEnforcementOptionsValidator(
	IEnumerable<RetentionPolicyDeclaration> declarations,
	IEnumerable<IRetentionContributor> contributors) : IValidateOptions<RetentionEnforcementOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RetentionEnforcementOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.ScanInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(RetentionEnforcementOptions.ScanInterval)} must be greater than zero.");
		}

		if (options.BatchSize < 1)
		{
			failures.Add($"{nameof(RetentionEnforcementOptions.BatchSize)} must be greater than zero.");
		}

		if (options.Enabled)
		{
			var registered = contributors.ToList();
			if (registered.Count == 0)
			{
				failures.Add(
					"Retention enforcement is enabled but no IRetentionContributor is registered, so nothing can be deleted. "
					+ "Register a contributor (for example AddOutboxRetention()), or set "
					+ $"{nameof(RetentionEnforcementOptions.Enabled)} to false.");
			}
			else if (!declarations.Any() && registered.Exists(static c => c.ConsumesDeclaredPolicies))
			{
				failures.Add(
					"Retention enforcement is enabled but no retention scope is declared, and at least one registered contributor "
					+ "deletes according to declared policies. Declare the types whose retention is enforced with "
					+ "AddRetentionPolicies<T>() or AddRetentionPoliciesFromAssembly(assembly), or set "
					+ $"{nameof(RetentionEnforcementOptions.Enabled)} to false.");
			}
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}

}
