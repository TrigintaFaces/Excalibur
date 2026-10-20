// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Fails start-up when authorization caching has been composed without an application-scoped distributed
/// cache to write into.
/// </summary>
/// <remarks>
/// <para>
/// Authorization grants are cached under keys derived only from the user id, because deciding which
/// application a cache entry belongs to is the cache registration's job rather than the key builder's.
/// That makes the scoped cache a prerequisite rather than an enhancement: without it, two applications
/// sharing one cache server address the same entry for the same user, and one serves the other's grants.
/// </para>
/// <para>
/// The dependency is <b>not</b> structural, and that is why this type exists. The components declare the
/// cache as <c>IDistributedCache? cache = null</c>, so a container composed without the scoped cache
/// builds and resolves them: nothing fails until <see cref="RequireCache"/> rejects the null while the
/// component is being constructed. <c>ValidateOnBuild</c> does not catch it either, because the keyed
/// descriptor is a factory it never constructs.
/// </para>
/// <para>
/// So the remedy message is only half of what this validator provides. The other half is <i>when</i> it
/// is raised: without the hosted-service registration the first indication of a misconfigured host is a
/// failed authorization on a live request, which for an authorization component is the worst place to
/// discover it. Removing this type does not merely degrade an error message — it moves the failure from
/// start-up to the first authorized request.
/// </para>
/// <para>
/// It probes the built provider's registrations without constructing the cache or performing I/O, so it is safe to run twice and
/// safe against a container that was built but never started.
/// </para>
/// </remarks>
internal sealed class AuthorizationCachePrerequisiteValidator
	: IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _provider;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizationCachePrerequisiteValidator"/> class.
	/// </summary>
	/// <param name="provider">The built provider whose cache registrations are checked.</param>
	public AuthorizationCachePrerequisiteValidator(IServiceProvider provider) =>
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));

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
		// Probe the provider snapshot without constructing the keyed cache. Mutating the source
		// collection after build cannot satisfy a dependency absent from this provider.
		var registered = _provider.GetRequiredService<IServiceProviderIsKeyedService>()
			.IsKeyedService(typeof(IDistributedCache), DistributedCacheServiceKeys.ApplicationScoped);

		if (registered)
		{
			return;
		}

		// THREE facts, because following the old message failed twice more before it worked. The package
		// is named first: this assembly does not reference Excalibur.Dispatch.Caching, so the call below
		// does not compile in a consumer holding only what authorization brought them, and an instruction
		// a reader cannot type is not a remedy. TWO registrations are then named, not one, and that
		// omission was not cosmetic either. The scoped cache is a
		// keyed wrapper that resolves the UNKEYED IDistributedCache at construction, and nothing in this
		// framework registers one by default. A consumer who added only the wrapper got a container that
		// BUILDS -- the keyed descriptor is a factory, which ValidateOnBuild does not construct -- and then
		// failed on the first authorized request instead. Naming one of the two calls turned a
		// composition-time failure into a request-time one, which is the opposite of this type's purpose.
		throw MissingCache();
	}

	internal static IDistributedCache RequireCache(IDistributedCache? cache) => cache ?? throw MissingCache();

	private static InvalidOperationException MissingCache() => new(
			"Authorization caching requires an application-scoped distributed cache, and none is "
			+ "registered. Authorization cache keys identify a user but not an application, so an "
			+ "unscoped cache shared by two applications would let one read the other's grants. Reference "
			+ "the Excalibur.Dispatch.Caching package, which this one does not depend on and which declares "
			+ "the second call below. Then TWO registrations: a base cache -- "
			+ "services.AddDistributedMemoryCache() or "
			+ "services.AddStackExchangeRedisCache(...) -- and then "
			+ "services.AddApplicationScopedDistributedCache(o => o.Scope = \"<this application>\"), which "
			+ "partitions that base cache's keyspace. The scoped registration wraps whichever unkeyed cache "
			+ "the container holds, so on its own it has nothing to wrap. Give a scope that is stable for "
			+ "this application and distinct from any other sharing the cache server -- the configured "
			+ "application name is the usual choice.");
}
