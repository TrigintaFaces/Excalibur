// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Soc2;

/// <summary>
/// Warns at startup when a Trust Services category is enabled that no registered
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
/// So the gap is reported where it is caused, and it is reported rather than enforced. Validators are
/// opt-in: a category nothing assesses is a legitimate, reportable state, so this is a diagnostic and
/// never a startup failure. The honest not-assessed statement in the generated document remains the
/// contract.
/// </para>
/// <para>
/// <b>Why a hosted service and not <see cref="IValidateOptions{TOptions}"/>.</b> Options validation
/// validates the options VALUE; whether an <see cref="IControlValidator"/> is registered is a property
/// of the service graph, which the options pipeline never reads. Microsoft draws the same line: options
/// validation is value-only, and the host runs graph-level checks at start —
/// <c>OptionsBuilder.ValidateOnStart</c> is itself implemented as an <c>IStartupValidator</c> invoked
/// from <c>Host.StartAsync</c>, and ASP.NET Core Data Protection reports its own missing optional
/// collaborator (no <c>IXmlEncryptor</c> configured) as a logged warning rather than a refusal. The
/// in-package shape this follows is <c>DevEncryptionWarningLogger</c>.
/// </para>
/// <para>
/// <b>Partial coverage is deliberately NOT reported.</b> Covering some controls of a category is the
/// ordinary case for an opt-in model, and warning there would train consumers doing the work to ignore
/// the log. The line is drawn at zero.
/// </para>
/// </remarks>
internal sealed partial class Soc2CoverageStartupDiagnostic : IHostedService
{
	private readonly IEnumerable<IControlValidator> _validators;
	private readonly IOptions<Soc2Options> _options;
	private readonly ILogger<Soc2CoverageStartupDiagnostic> _logger;

	public Soc2CoverageStartupDiagnostic(
		IEnumerable<IControlValidator> validators,
		IOptions<Soc2Options> options,
		ILogger<Soc2CoverageStartupDiagnostic> logger)
	{
		ArgumentNullException.ThrowIfNull(validators);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_validators = validators;
		_options = options;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		var covered = _validators
			.SelectMany(v => v.SupportedCriteria)
			.ToHashSet();

		foreach (var category in _options.Value.EnabledCategories ?? [])
		{
			if (!category.GetCriteria().Any(covered.Contains))
			{
				LogCategoryWithoutCoverage(_logger, category);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(
		ComplianceEventId.Soc2CategoryWithoutValidatorCoverage,
		LogLevel.Warning,
		"EnabledCategories includes {Category}, but no registered IControlValidator supports any "
		+ "criterion in it, so a generated report will enumerate {Category} and be able to assess none "
		+ "of it — every criterion in it renders as not assessed. Register a validator that supports "
		+ "{Category} — AddSoc2ComplianceWithBuiltInValidators registers the built-in set — or remove "
		+ "{Category} from EnabledCategories.")]
	private static partial void LogCategoryWithoutCoverage(ILogger logger, TrustServicesCategory category);
}
