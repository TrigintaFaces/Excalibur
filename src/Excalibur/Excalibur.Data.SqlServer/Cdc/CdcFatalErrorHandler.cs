// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Handles fatal errors that occur during CDC processing.
/// </summary>
/// <param name="exception">The exception that occurred during processing.</param>
/// <param name="failedEvent">The data change event that failed to process.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task CdcFatalErrorHandler(Exception exception, DataChangeEvent failedEvent);
