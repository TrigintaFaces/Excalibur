// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.AuditLogging;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security.Auditing;

/// <summary>
/// Startup validation that fails fast when audit log integrity is required
/// (<see cref="AuditOptions.EnsureLogIntegrity"/> is <see langword="true"/>) but the configured
/// <see cref="IAuditSigningKeyProvider"/> cannot produce a signing key.
/// </summary>
/// <remarks>
/// <para>
/// The check is provider-agnostic on purpose: it resolves an actual key via
/// <see cref="IAuditSigningKeyProvider.GetCurrentSigningKeyAsync"/> rather than inspecting a specific
/// options value, so it works both for the default options-backed provider and for a
/// KMS/secret-manager-backed provider (which legitimately leaves the local key config empty and produces
/// the key asynchronously). Surfacing the misconfiguration at startup — instead of on the first audit
/// write — prevents a running host from silently failing to protect audit-log integrity.
/// </para>
/// <para>
/// Expressed as <see cref="IValidateOptions{TOptions}"/> plus <c>ValidateOnStart()</c>, the Microsoft-first
/// shape for start-up configuration validation (<c>AddSecurityMonitoring</c> in the same file uses the
/// identical pattern for <c>SecurityMonitoringOptions</c>), rather than a hand-rolled <c>IHostedService</c>.
/// </para>
/// </remarks>
internal sealed class AuditSigningKeyStartupProbe : IValidateOptions<AuditOptions>
{
	private readonly IAuditSigningKeyProvider _signingKeyProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditSigningKeyStartupProbe"/> class.
	/// </summary>
	/// <param name="signingKeyProvider">The signing-key provider probed for key availability.</param>
	public AuditSigningKeyStartupProbe(IAuditSigningKeyProvider signingKeyProvider)
	{
		ArgumentNullException.ThrowIfNull(signingKeyProvider);

		_signingKeyProvider = signingKeyProvider;
	}

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AuditOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.EnsureLogIntegrity)
		{
			return ValidateOptionsResult.Success;
		}

		byte[] key;
		try
		{
			// IValidateOptions.Validate is synchronous by contract, and GetCurrentSigningKeyAsync is the
			// only way to ask a provider-agnostic signing-key provider for a key -- a KMS/secret-manager
			// provider legitimately produces one asynchronously. This runs exactly once, at start-up, off
			// any request path, which is the narrow case the DI composition root already accepts this
			// bridge for (see RabbitMQTransportServiceCollectionExtensions.ExecuteSync).
#pragma warning disable RS0030 // Sync-over-async bridge is constrained to start-up validation, off the hot path.
			(_, key) = Task.Run(
				async () => await _signingKeyProvider.GetCurrentSigningKeyAsync(CancellationToken.None)
					.ConfigureAwait(false)).GetAwaiter().GetResult();
#pragma warning restore RS0030
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AuditOptions)}.{nameof(AuditOptions.EnsureLogIntegrity)} is true, but the configured " +
				"IAuditSigningKeyProvider could not produce a signing key at startup. Configure " +
				"AuditIntegrityOptions.SigningKey, or register a signing-key provider (for example " +
				$"KMS/secret-manager-backed) that can supply a key. ({ex.Message})");
		}

		if (key is null || key.Length == 0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(AuditOptions)}.{nameof(AuditOptions.EnsureLogIntegrity)} is true, but the configured " +
				"IAuditSigningKeyProvider returned an empty signing key at startup. Configure a non-empty audit " +
				"signing key.");
		}

		return ValidateOptionsResult.Success;
	}
}
