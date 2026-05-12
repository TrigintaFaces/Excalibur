// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace Excalibur.Jobs.Azure.Internal;

/// <summary>
/// Default <see cref="IArmClientSeam"/> implementation that forwards to a
/// real <see cref="ArmClient"/>. This adapter is the only place in the
/// jobs path that touches the live Azure Resource Manager SDK client type
/// — tests substitute at the seam, never at the SDK type directly
/// (ADR-142 §D7).
/// </summary>
internal sealed class ArmClientAdapter : IArmClientSeam
{
	private readonly ArmClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ArmClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Azure Resource Manager client.</param>
	public ArmClientAdapter(ArmClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public Task<SubscriptionResource> GetDefaultSubscriptionAsync(CancellationToken cancellationToken)
		=> _inner.GetDefaultSubscriptionAsync(cancellationToken);
}
