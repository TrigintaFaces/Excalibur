// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Ambient, async-flow-local store for the current tenant id. A tenant is established for a logical
/// operation with <see cref="BeginScope"/> (a <see langword="using"/> scope); <see cref="ITenantContext"/>
/// reads it without a setter, so the ambient tenant is structurally read-only to consumers.
/// </summary>
public static class TenantContextHolder
{
	private static readonly AsyncLocal<string?> Ambient = new();

	/// <summary>Gets the current ambient tenant id, or <see langword="null"/> when none is established.</summary>
	/// <value>The ambient tenant id, or <see langword="null"/>.</value>
	public static string? Current => Ambient.Value;

	/// <summary>
	/// Gets an <see cref="ITenantContext"/> view over the ambient tenant, for a caller that needs the
	/// context type rather than the raw identifier — a store that resolves its partition from a context,
	/// or a conformance suite that varies the ambient scope around one store instance.
	/// </summary>
	/// <value>
	/// A shared, stateless reader. It holds no state of its own: every read resolves whatever scope
	/// <see cref="BeginScope"/> has established on the calling async flow at that moment, so one instance
	/// is correct for every tenant and is safe to hold as a singleton.
	/// </value>
	/// <remarks>
	/// <para>
	/// <strong>Outside any scope this resolves the reserved untenanted partition, never
	/// <see langword="null"/>.</strong> "No tenant was established" is a partition this framework names
	/// (<see cref="TenantScope.UntenantedSentinel"/>), not an undecided state, and reads and writes there
	/// are confined to it exactly as a real tenant's are. <see cref="ITenantContext.HasTenant"/> is
	/// therefore always <see langword="true"/>: a caller is always addressing some partition.
	/// </para>
	/// <para>
	/// Resolving <see langword="null"/> instead would mean "no decision was made", which a store that
	/// requires a tenant refuses outright. A reader written as a bare <see cref="Current"/> read therefore
	/// fails every operation that runs outside a scope, not only the tenant-scoped ones — which is why
	/// this obligation is stated here rather than left to each caller to rediscover.
	/// </para>
	/// <para>
	/// A host that <em>wants</em> that refusal wants the registered context instead:
	/// <c>AddTenantContext()</c> registers one that leaves an unresolved tenant unresolved, so a
	/// tenant-required store fails closed rather than writing to the untenanted partition. Use this
	/// property where the untenanted partition is a legitimate destination, not to silence that guard.
	/// </para>
	/// </remarks>
	public static ITenantContext AmbientContext { get; } = new AmbientOrUntenantedContext();

	/// <summary>
	/// Establishes <paramref name="tenantId"/> as the ambient tenant for the current async flow until the
	/// returned scope is disposed, when the previous ambient tenant is restored (scopes nest correctly).
	/// </summary>
	/// <param name="tenantId">The tenant id to make ambient, or <see langword="null"/> to clear it in-scope.</param>
	/// <returns>A scope that restores the previous ambient tenant on disposal.</returns>
	/// <remarks>
	/// <para>
	/// Over-length is refused here rather than at the point of use. An id no provider can store whole
	/// would otherwise establish cleanly, read back as present, and throw later in some store — far from
	/// the call that established it and with nothing left to say who did. Refusing at establishment keeps
	/// the failure where the caller still has the context to say what they meant.
	/// </para>
	/// <para>
	/// The other two values the conversion rejects are deliberately <em>accepted</em> here, and this is
	/// the distinction worth stating because the symmetric-looking rule is wrong. A blank id resolves to
	/// the untenanted partition rather than to a tenant named by whitespace, and the reserved sentinel is
	/// a storage encoding a store hands straight back on read — refusing either would turn a legitimate
	/// value into a throw. This ingress is not the tenant-naming guard; it establishes an ambient term,
	/// and "no tenant" is one of the terms it must be able to carry.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="tenantId"/> is longer than <see cref="TenantId.MaxLength"/> characters, which no
	/// shipped provider can store whole.
	/// </exception>
	public static IDisposable BeginScope(string? tenantId)
	{
		if (tenantId is { Length: > TenantId.MaxLength })
		{
			throw new ArgumentException(
				$"Tenant identifier exceeds the maximum length of {TenantId.MaxLength} characters supported by every shipped provider.",
				nameof(tenantId));
		}

		var previous = Ambient.Value;
		Ambient.Value = tenantId;
		return new AmbientScope(previous);
	}

	private sealed class AmbientOrUntenantedContext : ITenantContext
	{
		public string? TenantId => string.IsNullOrWhiteSpace(Ambient.Value)
			? TenantScope.UntenantedSentinel
			: Ambient.Value;

		// True in both states: a tenant is established, or the untenanted partition is, and that is a
		// partition. Reporting no tenant here would invite a caller to omit the partition term entirely.
		public bool HasTenant => true;
	}

	private sealed class AmbientScope(string? previous) : IDisposable
	{
		private volatile bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			Ambient.Value = previous;
		}
	}
}
