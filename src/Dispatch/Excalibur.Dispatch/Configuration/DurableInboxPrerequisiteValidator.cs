// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fails start-up when the durable inbox has been switched on but no <see cref="IInboxStore"/> is
/// registered to provide it.
/// </summary>
/// <remarks>
/// <para>
/// Enabling the inbox selects durable, store-backed deduplication in place of the in-memory kind. The
/// store itself is a persistence concern and lives in a separate package, so this one cannot register a
/// default: a consumer who asks for the durable inbox and never registers a store would otherwise get
/// neither, silently, and learn of it only when a redelivery was handled twice in production.
/// </para>
/// <para>
/// The check is inert unless the durable inbox was requested, so an application that never enables it is
/// unaffected. It reads service descriptors and performs no resolution and no I/O, which keeps it safe to
/// run twice and safe to run against a container that was built but never started.
/// </para>
/// </remarks>
internal sealed class DurableInboxPrerequisiteValidator
	: IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceCollection _services;
	private readonly IOptions<DispatchOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="DurableInboxPrerequisiteValidator"/> class.
	/// </summary>
	/// <param name="services">The service collection the application was composed from.</param>
	/// <param name="options">The dispatch options declaring whether the durable inbox was requested.</param>
	public DurableInboxPrerequisiteValidator(
		IServiceCollection services,
		IOptions<DispatchOptions> options)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Validate()
	{
		if (!_options.Value.Inbox.Enabled)
		{
			return;
		}

		// Read descriptors rather than resolving. The non-keyed contract is aliased to the keyed "default"
		// registration with GetRequiredKeyedService, so resolving it when no store exists throws instead of
		// returning null — which would turn this check's own probe into the failure it is meant to report.
		// HasStoreRegistrationFor discounts that alias and counts only descriptors that supply a store.
		if (_services.HasStoreRegistrationFor(typeof(IInboxStore)))
		{
			return;
		}

		throw new InvalidOperationException(
			"The durable inbox is enabled but no IInboxStore is registered, so nothing would persist " +
			"delivery state and duplicate deliveries would not be suppressed across a restart. Register an " +
			"inbox store — for example AddInbox(i => i.UseSqlServer(...)), i.UsePostgres(...), or " +
			"i.UseInMemory() for development — or leave the inbox disabled to use in-memory deduplication.");
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
