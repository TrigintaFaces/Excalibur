// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security.Auditing;

/// <summary>
/// Startup probe that fails the host fast when audit log integrity is required
/// (<see cref="AuditOptions.EnsureLogIntegrity"/> is <see langword="true"/>) but the configured
/// <see cref="IAuditSigningKeyProvider"/> cannot produce a signing key.
/// </summary>
/// <remarks>
/// The check is provider-agnostic on purpose: it resolves an actual key via
/// <see cref="IAuditSigningKeyProvider.GetCurrentSigningKeyAsync"/> rather than inspecting a specific
/// options value, so it works both for the default options-backed provider and for a
/// KMS/secret-manager-backed provider (which legitimately leaves the local key config empty and produces
/// the key asynchronously). Surfacing the misconfiguration at startup — instead of on the first audit
/// write — prevents a running host from silently failing to protect audit-log integrity.
/// </remarks>
internal sealed class AuditSigningKeyStartupProbe : IHostedService
{
	private readonly IOptions<AuditOptions> _auditOptions;
	private readonly IAuditSigningKeyProvider _signingKeyProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditSigningKeyStartupProbe"/> class.
	/// </summary>
	/// <param name="auditOptions">The audit options carrying <see cref="AuditOptions.EnsureLogIntegrity"/>.</param>
	/// <param name="signingKeyProvider">The signing-key provider probed for key availability.</param>
	public AuditSigningKeyStartupProbe(
		IOptions<AuditOptions> auditOptions,
		IAuditSigningKeyProvider signingKeyProvider)
	{
		ArgumentNullException.ThrowIfNull(auditOptions);
		ArgumentNullException.ThrowIfNull(signingKeyProvider);

		_auditOptions = auditOptions;
		_signingKeyProvider = signingKeyProvider;
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_auditOptions.Value.EnsureLogIntegrity)
		{
			return;
		}

		byte[] key;
		try
		{
			(_, key) = await _signingKeyProvider.GetCurrentSigningKeyAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			throw new InvalidOperationException(
				"AuditOptions.EnsureLogIntegrity is true, but the configured IAuditSigningKeyProvider could not " +
				"produce a signing key at startup. Configure AuditIntegrityOptions.SigningKey, or register a " +
				"signing-key provider (for example KMS/secret-manager-backed) that can supply a key.",
				ex);
		}

		if (key is null || key.Length == 0)
		{
			throw new InvalidOperationException(
				"AuditOptions.EnsureLogIntegrity is true, but the configured IAuditSigningKeyProvider returned an " +
				"empty signing key at startup. Configure a non-empty audit signing key.");
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
