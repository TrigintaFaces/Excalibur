// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Ambient, async-flow-local store for the current tenant id. A tenant is established for a logical
/// operation with <see cref="BeginScope"/> (a <see langword="using"/> scope); <see cref="ITenantContext"/>
/// reads it without a setter, so the ambient tenant is structurally read-only to consumers.
/// </summary>
public static class TenantContextHolder
{
	/// <summary>
	/// The <see cref="IMessageContext.Items"/> key a producer/transport sets to carry the tenant id into a
	/// dispatched message, which <c>ITenantResolver</c> reads on the receiving side.
	/// </summary>
	public const string TenantIdItemKey = "excalibur.dispatch.tenant-id";

	private static readonly AsyncLocal<string?> Ambient = new();

	/// <summary>Gets the current ambient tenant id, or <see langword="null"/> when none is established.</summary>
	/// <value>The ambient tenant id, or <see langword="null"/>.</value>
	public static string? Current => Ambient.Value;

	/// <summary>
	/// Establishes <paramref name="tenantId"/> as the ambient tenant for the current async flow until the
	/// returned scope is disposed, when the previous ambient tenant is restored (scopes nest correctly).
	/// </summary>
	/// <param name="tenantId">The tenant id to make ambient, or <see langword="null"/> to clear it in-scope.</param>
	/// <returns>A scope that restores the previous ambient tenant on disposal.</returns>
	public static IDisposable BeginScope(string? tenantId)
	{
		var previous = Ambient.Value;
		Ambient.Value = tenantId;
		return new AmbientScope(previous);
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
