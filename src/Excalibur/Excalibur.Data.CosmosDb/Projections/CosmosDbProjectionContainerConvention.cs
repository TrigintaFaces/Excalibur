// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Data.CosmosDb.Projections;

/// <summary>
/// Resolves Cosmos DB container names and options identities for projection stores using the configured
/// naming convention.
/// </summary>
/// <remarks>
/// <para>
/// Use this class when building custom repositories (e.g. extending <see cref="CosmosDbRepositoryBase{TDocument}"/>)
/// that need to target the same container as an <c>IProjectionStore&lt;T&gt;</c>. This ensures both the projection
/// store and your custom repository resolve to the same container name from a single source of truth.
/// </para>
/// <para>
/// Each projection type's store settings live in their own named options instance, keyed by
/// <see cref="GetOptionsName{TProjection}"/> — so read them through
/// <c>IOptionsMonitor&lt;CosmosDbProjectionStoreOptions&gt;.Get(name)</c>, never through the unnamed
/// <c>IOptions&lt;T&gt;</c> instance, which carries defaults no registration configured.
/// </para>
/// <para>
/// The default container name is <c>"projections"</c> (from <see cref="CosmosDbProjectionStoreOptions.ContainerName"/>),
/// so projections that do not override it share one container and are distinguished by the
/// <c>projectionType</c> partition key. A projection that does override it gets its own container, and two
/// projections configured in one container then still resolve to distinct databases/containers as configured.
/// </para>
/// </remarks>
public static class CosmosDbProjectionContainerConvention
{
	/// <summary>
	/// The name of the options instance a projection's store settings live under.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>The options name — the projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// Use this rather than writing <c>nameof(MyProjection)</c> at the call site: the registration, the
	/// store and every consumer repository reading the same settings must derive the key from one place.
	/// The full type name is used so two projections with the same simple name in different namespaces get
	/// separate settings instead of silently sharing one database and container.
	/// </remarks>
	public static string GetOptionsName<TProjection>()
		where TProjection : class => typeof(TProjection).FullName ?? typeof(TProjection).Name;

	/// <summary>
	/// Resolves the Cosmos DB container name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="options">The projection store options containing container name configuration.</param>
	/// <returns>The resolved container name (e.g. <c>"projections"</c>).</returns>
	/// <remarks>
	/// The projection's declared generation participates in the name, so a bump addresses a new container.
	/// </remarks>
	public static string GetContainerName<TProjection>(CosmosDbProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetContainerName(options, typeof(TProjection));
	}

	/// <summary>
	/// Resolves the Cosmos DB container name for a projection type using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing container name configuration.</param>
	/// <param name="projectionType">The projection type whose container name is resolved.</param>
	/// <returns>The resolved container name (e.g. <c>"projections-v2"</c>).</returns>
	/// <remarks>
	/// <para>
	/// Prefer this over the overload taking a type NAME. A generation is declared on the type, so a
	/// caller holding only a name cannot resolve one and will address the first generation's container
	/// whatever the type declares.
	/// </para>
	/// <para>
	/// THE DECLARED GENERATION PARTICIPATES IN THE CONTAINER, NOT THE DATABASE. A bump must address a
	/// distinct destination, and the container is the one segment of that destination this framework
	/// creates: the database is provisioned by the consumer, whereas a container is created on first use
	/// when configured to be. So the new generation folds from zero into its own container inside the
	/// database already configured, while the previous generation keeps its items and keeps serving reads
	/// until the consumer cuts over. The hyphen is legal in a container name, which permits any
	/// character other than "/", "\", "#", "?" and a trailing space.
	/// </para>
	/// <para>
	/// A projection declaring no generation, or the first, resolves to exactly the container it resolves
	/// to without this feature.
	/// </para>
	/// </remarks>
	public static string GetContainerName(CosmosDbProjectionStoreOptions options, Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(projectionType);

		// Applied to the fully resolved name, so a configured ContainerName override is versioned the
		// same way the default is -- a consumer who named their container does not thereby opt out of
		// generations.
		return ProjectionVersionConvention.Qualify(
			GetContainerName(options, projectionType.Name),
			projectionType,
			'-');
	}

	/// <summary>
	/// Resolves the Cosmos DB container name for a projection type name using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing container name configuration.</param>
	/// <param name="projectionTypeName">The projection type name (e.g. <c>"OrderSummary"</c>).</param>
	/// <returns>The resolved container name (e.g. <c>"projections"</c>).</returns>
	public static string GetContainerName(CosmosDbProjectionStoreOptions options, string projectionTypeName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionTypeName);

		// Cosmos DB uses a shared container for all projection types (unlike ES which uses per-type indices).
		// The projectionType partition key discriminates between types.
		return options.ContainerName;
	}
}
