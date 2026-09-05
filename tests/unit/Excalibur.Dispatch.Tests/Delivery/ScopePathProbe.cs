// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// Counts entries into the scope-taking branch of handler resolution.
/// </summary>
/// <remarks>
/// <para>
/// <c>HandlerScopeResolver.RunAsync</c> is the seam every scoped dispatch funnels through. It reads
/// <see cref="CurrentServiceProvider"/> immediately before falling through to
/// <c>_scopeFactory.CreateAsyncScope()</c> (<c>HandlerScopeResolver.cs:180-188</c>), so a read here means
/// the dispatch reached the point of taking a scope. Returning <see langword="null"/> keeps the real
/// behaviour: no ambient scope is offered, so the resolver goes on to create one exactly as it would.
/// </para>
/// <para>
/// This is the substitutable seam. <see cref="Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"/>
/// is NOT: Microsoft DI resolves it from an internal call site that is consulted before any user
/// descriptor, so a registered replacement is never handed out and a counter built on it silently
/// observes nothing.
/// </para>
/// </remarks>
internal sealed class ScopePathProbe : IDispatchAmbientScopeAccessor
{
	private int _scopePathEntries;

	/// <summary>Gets the number of times a dispatch reached the scope-taking branch.</summary>
	public int ScopePathEntries => Volatile.Read(ref _scopePathEntries);

	/// <inheritdoc />
	public IServiceProvider? CurrentServiceProvider
	{
		get
		{
			_ = Interlocked.Increment(ref _scopePathEntries);
			return null;
		}
	}
}
