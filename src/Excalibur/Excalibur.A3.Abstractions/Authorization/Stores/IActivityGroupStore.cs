// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Provider-neutral store for activity group persistence.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IRoleStore&lt;TRole&gt;</c> pattern:
/// minimal CRUD surface (5 methods) with <see cref="IServiceProvider.GetService(Type)"/> for ISP extensions.
/// </para>
/// <para>
/// Replaces <c>IActivityGroupRequestProvider</c> (4-method, SQL-coupled interface).
/// </para>
/// <para>
/// <b>TYPE INVARIANT — an activity group name is unique only WITHIN a tenant.</b> Two tenants may each
/// hold a group called <c>"Support"</c>, conferring different activities, and they are different groups.
/// Every member below is specified against that invariant; it is the property the whole surface turns on,
/// so it is stated here rather than left to each implementation to restate.
/// </para>
/// <para>
/// <b>KEY COMPOSITION — how a returned key is formed, stated so a caller can construct one.</b> Members
/// that return keyed results compose them with
/// <see cref="Excalibur.Dispatch.SegmentedKey.Compose(string, string)"/> — <b>two segments, in the order
/// (tenant identifier, group name)</b>. That composer escapes each segment before joining, so a tenant
/// identifier or group name containing the separator cannot collide with a different pair. <b>Do not build
/// such a key by string interpolation.</b> A hand-built <c>"tenant:name"</c> is correct only until a term
/// contains the separator, at which point it silently addresses nothing — and the one thing a caller must
/// get right to stay inside its own tenant is exactly this. Use the composer, or
/// <see cref="Excalibur.Dispatch.SegmentedKey.Split(string, int)"/> to decompose one.
/// </para>
/// <para>
/// <b>Arity is per member and is stated on each.</b> An implementation may key its own internal storage
/// with more segments than it returns; what is specified here is the shape a CALLER observes, which is the
/// only shape a caller may rely on.
/// </para>
/// </remarks>
public interface IActivityGroupStore : IServiceProvider
{
	/// <summary>
	/// Checks whether <paramref name="tenantId"/> has an activity group with the given name.
	/// </summary>
	/// <remarks>
	/// <b>Answers only for the named tenant, and never observes another tenant's catalogue.</b> Because a
	/// group name is unique only within a tenant, a bare-name question has no single answer: two tenants
	/// may each hold a group called <c>"Support"</c>. The tenant term is therefore part of the question,
	/// not a filter applied to it.
	/// </remarks>
	/// <param name="tenantId">The tenant identifier. Required; must be non-empty. Pass a BARE value.</param>
	/// <param name="activityGroupName">The name of the activity group, BARE — not a composed key.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="tenantId"/> has a group so named; otherwise
	/// <see langword="false"/>. Modifies nothing.
	/// </returns>
	Task<bool> ActivityGroupExistsAsync(string tenantId, string activityGroupName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves <paramref name="tenantId"/>'s activity groups.
	/// </summary>
	/// <param name="tenantId">The tenant identifier. Required; must be non-empty. Pass a BARE value.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <paramref name="tenantId"/>'s activity groups, each keyed by its composed (tenant, name) key and
	/// carrying the names of the activities in that group. Never another tenant's groups.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Answers only for the named tenant.</b> The tenant term SELECTS and the composed key still
	/// IDENTIFIES; both are needed and they do different jobs. Without the selection every caller receives
	/// the whole estate's catalogue and can read it, and without the composition two tenants' same-named
	/// groups fuse into one entry whose membership satisfies both.
	/// </para>
	/// <para>
	/// <b>The value is typed, and that is load-bearing.</b> This returned <c>object</c> and was described
	/// as a "provider-specific projection", so each implementation invented its own shape: the in-memory
	/// store produced a <c>List&lt;string&gt;</c> while both database stores produced a <b>private</b>
	/// record declared inside the store. The single consumer then had to guess with a runtime type test,
	/// which only the in-memory shape satisfied, so on either database provider every activity group
	/// resolved to nothing -- with no exception and no log, because a failed type test is silent.
	/// </para>
	/// <para>
	/// A private type is not merely inconvenient to consume; it is impossible to consume. No caller could
	/// have written a correct test against it. Typing the value removes the question: a store that returns
	/// the wrong shape now fails to compile.
	/// </para>
	/// </remarks>
	Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> FindActivityGroupsAsync(
		string tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes <paramref name="tenantId"/>'s activity groups, and no other tenant's.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the member a consumer reaches for to remove its own groups.</b> Its sibling
	/// <see cref="ReplaceAllActivityGroupsAsync"/> is estate-wide; the two are separate NAMED operations
	/// rather than one member with an optional tenant, deliberately.
	/// </para>
	/// <para>
	/// <b>Why not one member taking a nullable tenant.</b> A <c>string?</c> whose null meant "every tenant"
	/// would make the CATASTROPHIC case the value you get by FORGETTING — an omitted argument, a default, an
	/// unassigned field all resolve to the widest possible blast radius. Two named operations make the wrong
	/// one inexpressible by accident: a caller that wants one tenant cannot reach the estate-wide behaviour
	/// without naming it.
	/// </para>
	/// <para>
	/// <b>And why the tenant is explicit rather than ambient.</b> Resolving it from an ambient scope would
	/// make the SAME SOURCE LINE mean different things depending on whether a scope happened to be active,
	/// and it fails silently in both directions — scoped when you meant estate-wide leaves an incomplete
	/// refresh, estate-wide when you meant scoped destroys every other tenant's groups. For an operation
	/// whose radius can be "everything", the radius must be readable AT THE CALL SITE.
	/// </para>
	/// </remarks>
	/// <param name="tenantId">The tenant whose groups to remove. Required; must be non-empty. Pass a BARE value.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected. Zero when the tenant had no groups.</returns>
	Task<int> DeleteActivityGroupsForTenantAsync(string tenantId, CancellationToken cancellationToken);

	/// <summary>
	/// Makes <paramref name="catalogue"/> the whole activity-group catalogue, for every tenant, in one atomic step.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This member is estate-wide by specification.</b> Afterwards the store holds exactly the entries of
	/// <paramref name="catalogue"/>: a tenant that is absent from it has no groups at all. To remove one
	/// tenant's groups, call <see cref="DeleteActivityGroupsForTenantAsync"/>, which names the tenant; the
	/// estate-wide behaviour is never reached by omitting an argument.
	/// </para>
	/// <para>
	/// <b>Atomic for readers and writers.</b> A concurrent reader observes either the whole previous catalogue
	/// or the whole new one, never a partial one. Concurrent replaces are serialized, so the catalogue that
	/// remains is the one whose replace committed last. If the operation fails, the previous catalogue is
	/// unchanged.
	/// </para>
	/// <para>
	/// <b>Preconditions are carried by the type.</b> An <see cref="ActivityGroupCatalogue"/> can only be
	/// constructed non-empty, as a set, with every term present, free of leading and trailing whitespace and
	/// within the shipped widths, so every store receives an input every other store would also accept.
	/// </para>
	/// <para>
	/// <b>One application per catalogue.</b> The catalogue carries no application identifier, so two
	/// applications that replace the same store's catalogue replace each other's groups. Give each application
	/// its own store, for example its own schema on the database providers.
	/// </para>
	/// <para>
	/// <b>It runs in its own transaction.</b> A database store refuses, with
	/// <see cref="InvalidOperationException"/>, to run inside an ambient transaction, because it would then
	/// commit or roll back with work it cannot see.
	/// </para>
	/// </remarks>
	/// <param name="catalogue">The complete catalogue to make current.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The distinct tenants that held groups immediately before the replace, observed under the same lock that
	/// orders the replace, in no particular order. A caller invalidating state derived from the old catalogue
	/// needs these as well as <see cref="ActivityGroupCatalogue.TenantIds"/>: a tenant that the replace removed
	/// appears only here.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="catalogue"/> is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a database store is called inside an ambient transaction, or cannot obtain the lock that
	/// orders replaces. Nothing is changed.
	/// </exception>
	Task<IReadOnlyCollection<string>> ReplaceAllActivityGroupsAsync(
		ActivityGroupCatalogue catalogue,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a new activity group entry.
	/// </summary>
	/// <param name="tenantId">
	/// The tenant identifier. Required; must be non-empty. Pass it as a BARE value — the store composes
	/// any key it needs internally. No member of this interface accepts an already-composed key.
	/// </param>
	/// <param name="name">The activity group name, BARE — unique only within <paramref name="tenantId"/>.</param>
	/// <param name="activityName">The activity name to associate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> CreateActivityGroupAsync(string tenantId, string name,
		string activityName, CancellationToken cancellationToken);

	/// <summary>
	/// Resolves an optional activity-group-store capability, or <see langword="null"/> when unavailable.
	/// </summary>
	/// <param name="serviceType">The capability interface to resolve, for example <c>typeof(IActivityGroupGrantStore)</c>.</param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this store provides the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// <b>Inherited from <see cref="IServiceProvider"/> rather than declared here</b>, matching
	/// <c>IGrantStore</c> and the BCL: the framework already has the interface for "resolve a service by
	/// type", so the store implements that one instead of a look-alike of it. A consumer can therefore hand
	/// this store to anything that accepts an <see cref="IServiceProvider"/>.
	/// <para>
	/// The default implementation answers for any capability this instance itself implements. Leaf stores
	/// need not override it. Decorators MUST override it to defer unknown capabilities to the store they
	/// wrap; a decorator that does not forward silently disables the capability beneath it.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceType"/> is null.</exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}
}
