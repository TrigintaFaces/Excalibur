// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Excalibur.Compliance;

namespace Excalibur.AuditLogging;

/// <summary>
/// Boot-time validation that the configured <see cref="IAuditStore" /> is durable, unless the host has
/// explicitly accepted a volatile one.
/// </summary>
/// <remarks>
/// <para>
/// This validates the <em>store's durability</em>, not the shape of an options object. An options type can
/// be perfectly well-formed while the store behind it silently discards everything written to it, so a
/// shape check would pass in exactly the case that matters.
/// </para>
/// <para>
/// It runs at startup rather than in a constructor so the failure arrives before the host serves traffic —
/// a first-audit-write failure is discovered by the first thing worth auditing, which is the worst possible
/// moment to learn the trail is volatile.
/// </para>
/// </remarks>
internal sealed class AuditStoreDurabilityValidator : IValidateOptions<AuditLoggingOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditStoreDurabilityValidator" /> class.
	/// </summary>
	/// <param name="services"> The provider used to inspect the configured audit-store registration. </param>
	public AuditStoreDurabilityValidator(IServiceProvider services) => _services = services;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AuditLoggingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.AllowVolatileAuditStore)
		{
			// The host said so out loud. Nothing to enforce.
			return ValidateOptionsResult.Success;
		}

		// Ask the registered store itself whether it is durable. A durable store answers for
		// IDurableAuditStore through IServiceProvider.GetService; a volatile one answers null. This
		// resolves the store that actually WON registration (including through a decorator, which
		// forwards the query), so registration order and wrapping are irrelevant — the object that
		// serves traffic is the object that is inspected.
		var store = _services.GetService<IAuditStore>();
		if (store?.GetService(typeof(IDurableAuditStore)) is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"Audit logging is configured with a volatile audit store, so the audit trail would be lost on " +
			"process restart while every write still reported success. Register a durable audit store (for " +
			"example the SQL Server or Postgres audit store), or, for development and test hosts only, accept " +
			$"the volatile store explicitly by setting " +
			$"{nameof(AuditLoggingOptions)}.{nameof(AuditLoggingOptions.AllowVolatileAuditStore)} to true.");
	}
}
