// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Identifies a projection's configured logical destination within its subscription scope.</summary>
/// <remarks>
/// Resolve this optional capability through the projection store's service-provider interface.
/// The identity is a nonblank, stable, provider-specific value without credentials or node topology.
/// SubscriptionScope must separately identify the source and destination cluster. Providers and custom
/// decorators without this capability rely on that consumer-supplied scope and revision for destination identity.
/// This is configuration identity, not a physical resource UUID or a lifecycle fence. Alias repointing,
/// index replacement, or recreation under the same name requires deliberate subscription/revision and rebuild handling.
/// </remarks>
public interface IProjectionDestinationIdentity
{
	/// <summary>Gets the configured logical target, qualified by its provider.</summary>
	/// <value>A nonblank stable identity within the consumer's subscription scope.</value>
	string Identity { get; }
}
