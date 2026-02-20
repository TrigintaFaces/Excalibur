// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Provides distributed coordination capabilities for job scheduling across multiple instances.
/// Composes <see cref="IJobLockProvider"/>, <see cref="IJobRegistry"/>, and <see cref="IJobDistributor"/>
/// for consumers that need the full coordination surface.
/// </summary>
public interface IJobCoordinator : IJobLockProvider, IJobRegistry, IJobDistributor;
