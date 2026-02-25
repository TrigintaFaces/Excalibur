// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Hosting;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Represents a hosted service that monitors job configurations and manages their execution lifecycle.
/// </summary>
/// <remarks>
/// This interface combines the functionalities of <see cref="IHostedService" /> for background processing and <see cref="IDisposable" />
/// for resource cleanup.
/// </remarks>
public interface IJobConfigHostedWatcherService : IHostedService, IDisposable;
