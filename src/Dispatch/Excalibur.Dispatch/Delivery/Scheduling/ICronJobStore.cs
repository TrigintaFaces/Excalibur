// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides persistent storage for recurring cron jobs.
/// </summary>
/// <remarks>
/// <para>
/// This is the composite interface that inherits from:
/// <list type="bullet">
/// <item><description><see cref="ICronJobStoreCrud"/> -- add, update, remove, get, and list jobs</description></item>
/// <item><description><see cref="ICronJobStoreQuery"/> -- due jobs, tag filtering, and execution history</description></item>
/// <item><description><see cref="ICronJobStoreOperations"/> -- scheduling updates, execution recording, and enable/disable</description></item>
/// </list>
/// </para>
/// </remarks>
public interface ICronJobStore : ICronJobStoreCrud, ICronJobStoreQuery, ICronJobStoreOperations
{
}
