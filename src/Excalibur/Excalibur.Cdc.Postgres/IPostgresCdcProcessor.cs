// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Defines the contract for a Postgres Change Data Capture (CDC) processor.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="ICdcStreamProcessor{TEvent, TPosition}"/>
/// with Postgres-specific types. All streaming and batch processing methods
/// are inherited from the base interfaces.
/// </para>
/// </remarks>
public interface IPostgresCdcProcessor : ICdcStreamProcessor<PostgresDataChangeEvent, PostgresCdcPosition>;
