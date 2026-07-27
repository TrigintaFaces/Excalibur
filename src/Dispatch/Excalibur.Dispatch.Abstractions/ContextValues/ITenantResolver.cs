// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Resolves the tenant identifier for an inbound message or request from its dispatch context.
/// </summary>
/// <remarks>
/// Implementations inspect the supplied <see cref="IMessageContext"/> (headers, claims, routing
/// metadata) and produce the tenant the message belongs to. The resolved value establishes the
/// ambient tenant scope surfaced by <see cref="ITenantContext"/>.
/// </remarks>
public interface ITenantResolver
{
	/// <summary>
	/// Resolves the tenant identifier for the supplied message context.
	/// </summary>
	/// <param name="context">The dispatch context of the inbound message or request.</param>
	/// <param name="cancellationToken">A token that cancels the resolution operation.</param>
	/// <returns>
	/// The resolved tenant identifier, or <see langword="null"/> when the context carries no tenant.
	/// </returns>
	System.Threading.Tasks.ValueTask<string?> ResolveAsync(IMessageContext context, System.Threading.CancellationToken cancellationToken);
}
