// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Defines the contract for a MongoDB Change Data Capture (CDC) processor.
/// </summary>
/// <remarks>
/// <para>
/// The processor uses MongoDB Change Streams to capture document changes
/// from a replica set or sharded cluster. This interface extends
/// <see cref="ICdcStreamProcessor{TEvent, TPosition}"/> with MongoDB-specific types.
/// All streaming and batch processing methods are inherited from the base interfaces.
/// </para>
/// <para>
/// Server requirements:
/// <list type="bullet">
/// <item><description>MongoDB 3.6+ for collection-level change streams</description></item>
/// <item><description>MongoDB 4.0+ for database-level or cluster-level change streams</description></item>
/// <item><description>MongoDB 6.0+ for pre-image support (FullDocumentBeforeChange)</description></item>
/// <item><description>Must be a replica set or sharded cluster (not standalone)</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IMongoDbCdcProcessor : ICdcStreamProcessor<MongoDbDataChangeEvent, MongoDbCdcPosition>;
