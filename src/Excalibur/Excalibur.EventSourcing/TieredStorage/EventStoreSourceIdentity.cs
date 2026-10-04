// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Identifies one captured provider binding without exposing connections, readers or event operations.
/// Reference identity establishes registration alignment, not the correctness of external routing.
/// </summary>
internal sealed class EventStoreSourceIdentity
{
}
