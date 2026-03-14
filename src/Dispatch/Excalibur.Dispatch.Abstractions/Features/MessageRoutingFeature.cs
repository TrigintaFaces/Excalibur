// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Default implementation of <see cref="IMessageRoutingFeature"/>.
/// </summary>
public sealed class MessageRoutingFeature : IMessageRoutingFeature
{
	/// <inheritdoc />
	public RoutingDecision? RoutingDecision { get; set; }

	/// <inheritdoc />
	public string? PartitionKey { get; set; }

	/// <inheritdoc />
	public string? Source { get; set; }
}
