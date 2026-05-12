// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Defines the contract for processing CosmosDb Change Feed events.
/// </summary>
/// <remarks>
/// <para>
/// Supports two processing modes via the base interfaces:
/// </para>
/// <list type="bullet">
/// <item>
/// <term>Continuous</term>
/// <description>Use <see cref="ICdcStreamProcessor{TEvent, TPosition}.StartAsync"/> for long-running background processing.</description>
/// </item>
/// <item>
/// <term>Batch</term>
/// <description>Use <see cref="ICdcProcessor{TEvent}.ProcessBatchAsync"/> for serverless or on-demand scenarios.</description>
/// </item>
/// </list>
/// </remarks>
public interface ICosmosDbCdcProcessor : ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>;
