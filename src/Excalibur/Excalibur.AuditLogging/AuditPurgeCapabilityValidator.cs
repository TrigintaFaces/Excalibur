// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Excalibur.AuditLogging.Retention;
using Excalibur.Compliance;

namespace Excalibur.AuditLogging;

/// <summary>
/// Boot-time validation that the configured <see cref="IAuditStore" /> can purge, when retention
/// enforcement is enabled.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DefaultAuditRetentionService" /> resolves <see cref="IAuditPurgeCapability" /> from the
/// store and throws <see cref="NotSupportedException" /> when it is absent — correct, fail-loud
/// behaviour, but one that previously only fired on the first
/// <see cref="AuditRetentionOptions.CleanupInterval" /> elapsing (a day, by default) rather than at
/// startup. A host can look healthy for that whole window while nothing is ever purged.
/// </para>
/// <para>
/// This validates the <em>store's capability</em>, not the shape of an options object, for the same
/// reason <see cref="AuditStoreDurabilityValidator" /> does: an options type can be well-formed while
/// the store behind it silently cannot do what the options ask of it.
/// </para>
/// </remarks>
internal sealed class AuditPurgeCapabilityValidator : IValidateOptions<AuditRetentionOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditPurgeCapabilityValidator" /> class.
	/// </summary>
	/// <param name="services"> The provider used to inspect the configured audit-store registration. </param>
	public AuditPurgeCapabilityValidator(IServiceProvider services) => _services = services;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AuditRetentionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.EnableRetentionEnforcement)
		{
			// Enforcement is off, so DefaultAuditRetentionService never resolves the capability. Nothing to
			// gate -- the host already said so explicitly via the same option that would otherwise skip the
			// purge entirely.
			return ValidateOptionsResult.Success;
		}

		// Ask the registered store itself, the same way AuditStoreDurabilityValidator asks about
		// IDurableAuditStore: this resolves the store that actually won registration (including through a
		// decorator, which forwards the query), so registration order and wrapping are irrelevant.
		var store = _services.GetService<IAuditStore>();
		if (store?.GetService(typeof(IAuditPurgeCapability)) is IAuditPurgeCapability)
		{
			return ValidateOptionsResult.Success;
		}

		var storeTypeName = store?.GetType().FullName ?? "<no IAuditStore registered>";

		return ValidateOptionsResult.Fail(
			$"Audit retention enforcement is enabled, but the configured audit store ({storeTypeName}) does " +
			"not support purging expired events. Every cleanup interval would fail after this point, " +
			$"reporting the failure only after {nameof(AuditRetentionOptions.CleanupInterval)} elapses. " +
			"Register a purge-capable audit store, or disable enforcement explicitly by setting " +
			$"{nameof(AuditRetentionOptions)}.{nameof(AuditRetentionOptions.EnableRetentionEnforcement)} to false.");
	}
}
