// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// One activity conferred by one tenant's activity group: a single row of an activity-group catalogue.
/// </summary>
/// <param name="TenantId">The tenant the group belongs to, as a bare value.</param>
/// <param name="Name">The group's name, bare. Unique only within <paramref name="TenantId"/>.</param>
/// <param name="ActivityName">The activity the group confers.</param>
/// <remarks>
/// An entry carries no validation of its own, because <see langword="default"/> can always construct one.
/// It is validated when it is placed into an <see cref="ActivityGroupCatalogue"/>, which is the only form a
/// store accepts for a whole-catalogue replace.
/// </remarks>
public readonly record struct ActivityGroupEntry(string TenantId, string Name, string ActivityName);
