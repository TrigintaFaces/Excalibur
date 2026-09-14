// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Soc2;

/// <summary>
/// Fails startup when a Trust Services category is enabled that no registered
/// <see cref="IControlValidator"/> can assess.
/// </summary>
/// <remarks>
/// <para>
/// A generated report enumerates a section for every criterion in an enabled category, but can only
/// substantiate one where some registered validator supports a control for it. Those two sets are
/// independent, and when they diverge the report says so honestly — every uncovered criterion renders
/// as not assessed. <b>That is correct for the report and useless as a signal</b>: it tells the reader
/// of a finished attestation something the person who wrote the configuration needed to know.
/// </para>
/// <para>
/// So the gap is reported where it is caused. Enabling a category buys nothing unless a validator can
/// assess it, and a category with no coverage at all is a configuration mistake rather than a partial
/// result — the resulting document would assert nothing about it while appearing to cover it.
/// </para>
/// <para>
/// <b>Partial coverage is deliberately NOT an error.</b> Validators are opt-in and covering some
/// controls of a category is the ordinary case; failing there would make the opt-in model unusable and
/// would punish exactly the consumers doing the work. The line is drawn at zero.
/// </para>
/// </remarks>
internal sealed class Soc2CoverageOptionsValidator : IValidateOptions<Soc2Options>
{
	private readonly IEnumerable<IControlValidator> _validators;

	public Soc2CoverageOptionsValidator(IEnumerable<IControlValidator> validators) =>
		_validators = validators ?? throw new ArgumentNullException(nameof(validators));

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, Soc2Options options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var covered = _validators
			.SelectMany(v => v.SupportedCriteria)
			.ToHashSet();

		var failures = new List<string>();

		foreach (var category in options.EnabledCategories ?? [])
		{
			if (category.GetCriteria().Any(covered.Contains))
			{
				continue;
			}

			failures.Add(
				$"{nameof(Soc2Options.EnabledCategories)} includes {category}, but no registered "
				+ $"{nameof(IControlValidator)} supports any criterion in it, so a generated report "
				+ $"would enumerate {category} and be able to assess none of it. Register a validator "
				+ $"that supports {category} — AddSoc2ComplianceWithBuiltInValidators registers the "
				+ $"built-in set — or remove {category} from {nameof(Soc2Options.EnabledCategories)}.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
