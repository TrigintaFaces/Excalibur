// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Identifies one tiered registration without exposing its store or any event operations.
/// Forwarding this receipt assumes that the forwarding decorator preserves the inner read path.
/// </summary>
internal sealed class TieredStorageCompositionReceipt
{
}
