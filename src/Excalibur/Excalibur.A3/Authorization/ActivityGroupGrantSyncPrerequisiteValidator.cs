// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Fails start-up when the composed grant store cannot replace grants atomically and the host has asked for
/// an atomic sync; warns once when the host has accepted the non-atomic one.
/// </summary>
/// <remarks>
/// <para>
/// <b>This brings a decision forward; it does not make it.</b> The sync itself checks the store it is actually
/// handed, because that is the only place the answer is certain. What this adds is the start-up failure: a
/// consumer who composed Cosmos DB, DynamoDB, Firestore or MongoDB learns at composition time that grant
/// synchronization through that store is not atomic, rather than at the first sync.
/// </para>
/// <para>
/// <b>It reads descriptors and resolves nothing</b>, so it performs no I/O, opens no connection and is safe to
/// run twice — the contract <see cref="IStartupPrerequisiteValidator"/> states. The price is that a store
/// registered through a factory has no implementation type on its descriptor, and this check cannot see what
/// it would construct. Such a composition passes here and is decided at the sync instead. Every store this
/// framework ships registers by type or as an instance, so the check is not vacuous for any of them.
/// </para>
/// </remarks>
internal sealed class ActivityGroupGrantSyncPrerequisiteValidator
	: IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceCollection _services;
	private readonly IOptions<ActivityGroupSyncOptions> _options;
	private readonly ILogger<ActivityGroupGrantSyncPrerequisiteValidator> _logger;

	private int _warned;

	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityGroupGrantSyncPrerequisiteValidator"/> class.
	/// </summary>
	/// <param name="services">The service collection the application was composed from.</param>
	/// <param name="options">The configured sync options.</param>
	/// <param name="logger">The logger the one start-up warning is written to.</param>
	public ActivityGroupGrantSyncPrerequisiteValidator(
		IServiceCollection services,
		IOptions<ActivityGroupSyncOptions> options,
		ILogger<ActivityGroupGrantSyncPrerequisiteValidator> logger)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_services = services;
		_options = options;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	/// <inheritdoc />
	public void Validate()
	{
		var store = ComposedGrantStoreType();

		if (store is null || typeof(IActivityGroupGrantReplacement).IsAssignableFrom(store))
		{
			return;
		}

		if (_options.Value.GrantSyncAtomicity == GrantSyncAtomicity.BestEffort)
		{
			// Once: both this type's registrations resolve the same instance, so a host and a bare provider
			// validation do not produce two warnings about one composition.
			if (Interlocked.Exchange(ref _warned, 1) == 0)
			{
				_logger.LogGrantSyncNotAtomic(store.FullName ?? store.Name);
			}

			return;
		}

		throw new InvalidOperationException(
			$"{store.FullName ?? store.Name} cannot replace a set of activity-group grants in one step, and "
			+ $"{nameof(ActivityGroupSyncOptions)}.{nameof(ActivityGroupSyncOptions.GrantSyncAtomicity)} is "
			+ $"{nameof(GrantSyncAtomicity.Required)}. A grant sync through that store deletes the existing "
			+ "grants and then inserts the new ones, so a reader in between observes a partial set and is "
			+ "denied access the snapshot confers, and a sync that fails part-way leaves the set partial until "
			+ "the next one succeeds. Either compose a store that replaces atomically — the SQL Server, "
			+ "PostgreSQL and in-memory stores do — or accept the window explicitly with "
			+ $"services.Configure<{nameof(ActivityGroupSyncOptions)}>(o => o."
			+ $"{nameof(ActivityGroupSyncOptions.GrantSyncAtomicity)} = "
			+ $"{nameof(GrantSyncAtomicity)}.{nameof(GrantSyncAtomicity.BestEffort)}).");
	}

	/// <summary>
	/// Returns the type the container would construct for <see cref="IActivityGroupGrantStore"/>, or
	/// <see langword="null"/> when there is none or the descriptor does not name one.
	/// </summary>
	private Type? ComposedGrantStoreType()
	{
		// The LAST descriptor wins in this container, so an override registered after a provider's own is what
		// would actually be resolved. Reading the first would report the registration a consumer replaced.
		ServiceDescriptor? composed = null;

		foreach (var descriptor in _services)
		{
			if (descriptor.ServiceType == typeof(IActivityGroupGrantStore))
			{
				composed = descriptor;
			}
		}

		// Read through the keyed-safe accessors, never the raw getters. ServiceDescriptor's non-keyed
		// ImplementationType/ImplementationInstance throw for a keyed descriptor on .NET 8.x and mis-read
		// silently on net9/10 -- and a silent mis-read here reports the wrong composed store on an
		// authorization path, with nothing to say it happened.
		return composed?.GetImplementationType() ?? composed?.GetImplementationInstance()?.GetType();
	}
}
