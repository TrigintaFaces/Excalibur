// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Defines the contract for a SQL Server Change Data Capture (CDC) processor.
/// </summary>
/// <remarks>
/// <para>
/// SQL Server CDC is a poll-based provider that reads change tables. This interface
/// extends <see cref="ICdcProcessor{TEvent}"/> with SQL Server-specific types.
/// The batch processing method is inherited from the base interface.
/// </para>
/// </remarks>
public interface ISqlServerCdcProcessor : ICdcProcessor<DataChangeEvent>;
