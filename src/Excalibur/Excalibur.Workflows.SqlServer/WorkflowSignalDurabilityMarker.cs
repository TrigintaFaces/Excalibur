// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows.SqlServer;

/// <summary>
/// Registration-time implementation of <see cref="IWorkflowSignalDurability"/> declaring that the wired
/// <see cref="IWorkflowSignalInbox"/> is the durable SQL Server store. Registered <em>only</em> by
/// <see cref="M:Microsoft.Extensions.DependencyInjection.WorkflowSignalInboxSqlServerExtensions.AddSqlServerWorkflowSignalInbox(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{Excalibur.Workflows.SqlServer.SqlServerWorkflowSignalInboxOptions})"/>,
/// in the same call that wires <see cref="SqlServerWorkflowSignalInbox"/>, so the marker cannot be present
/// unless the durable inbox was actually registered. Consumed by the startup durability gate as a presence
/// signal (never resolved for behavior).
/// </summary>
internal sealed class WorkflowSignalDurabilityMarker : IWorkflowSignalDurability;
