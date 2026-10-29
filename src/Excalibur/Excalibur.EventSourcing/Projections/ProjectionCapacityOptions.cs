// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>Bounds framework projection storage work within one service provider.</summary>
/// <remarks>
/// Limits apply independently to each storage resource and are frozen when the governor is created.
/// They bound logical operations, not SDK connections, requests, or activity in other processes --
/// so N hosts running this configuration present up to N times these limits to the same storage.
/// Size them per process and multiply by your host count when sizing the storage side.
/// Unidentified resources share one conservative budget. User handlers execute outside storage admission.
/// </remarks>
public sealed class ProjectionCapacityOptions
{
    /// <summary>Gets or sets concurrent storage operations across all resources, in this process. The default is 16.</summary>
    /// <remarks>This backstop also covers differently configured aliases of the same storage system.</remarks>
    public int MaxConcurrentOperations { get; set; } = 16;

    /// <summary>Gets or sets concurrent storage reads per resource, in this process. The default is four.</summary>
    public int MaxConcurrentReads { get; set; } = 4;

    /// <summary>Gets or sets concurrent storage writes per resource, in this process. The default is four.</summary>
    public int MaxConcurrentWrites { get; set; } = 4;

    /// <summary>Gets or sets concurrent rebuilds per resource, in this process. The default is one.</summary>
    public int MaxConcurrentRebuilds { get; set; } = 1;

    /// <summary>Gets or sets waiting operations per resource and operation kind, in this process. The default is 256; zero disables waiting.</summary>
    public int QueueLimit { get; set; } = 256;

    /// <summary>Gets or sets the maximum distinct resources retained by the governor. The default is 1024.</summary>
    /// <remarks>Exceeding this bound is rejected. Active budgets are never evicted or replaced.</remarks>
    public int MaxResources { get; set; } = 1024;
}
