// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// Shared constants for the durable workflow engine.
/// </summary>
internal static class WorkflowConstants
{
    /// <summary>
    /// The event-store aggregate-type discriminator under which every workflow instance journal is stored.
    /// A workflow instance is an aggregate stream keyed by its instance identifier.
    /// </summary>
    internal const string JournalAggregateType = "WorkflowInstance";
}
