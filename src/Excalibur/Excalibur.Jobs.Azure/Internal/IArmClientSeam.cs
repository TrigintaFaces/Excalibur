// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace Excalibur.Jobs.Azure.Internal;

/// <summary>
/// Narrow internal seam over <see cref="ArmClient"/> used by
/// <see cref="AzureLogicAppsJobProvider"/>. Exposes only the use-case
/// operations needed by the job provider so tests can substitute at this
/// boundary without faking the concrete SDK client type (ADR-142 §D7).
/// </summary>
/// <remarks>
/// Follows the COMPASS S798 msg 1712 ruling: flat use-case methods, not
/// SDK topology mirroring. Data-shaped SDK types
/// (<see cref="SubscriptionResource"/>) cross the seam — they are
/// hierarchical resource handles and are safe to pass through.
/// </remarks>
internal interface IArmClientSeam
{
	/// <summary>
	/// Gets the default subscription for the authenticated tenant.
	/// </summary>
	Task<SubscriptionResource> GetDefaultSubscriptionAsync(CancellationToken cancellationToken);
}
