// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Serialises every Oracle-backed test class in this assembly onto ONE container.
/// </summary>
/// <remarks>
/// <para>
/// Before this existed, eight classes each took <c>IClassFixture&lt;OracleOutboxStoreContainerFixture&gt;</c>.
/// The fixture holds its container in an INSTANCE field, and <c>IClassFixture</c> is per class, so with the
/// shared runner settings (<c>parallelizeTestCollections: true</c>, <c>maxParallelThreads: 0</c>) that
/// started <b>eight Oracle containers concurrently on one machine</b>. The migration test was the one long
/// enough to be cancelled under that load: <c>ORA-00604 -&gt; ORA-01013 "User requested cancel"</c>, passing
/// when run alone and failing in the full assembly — a load-dependent failure, not an ordering one.
/// </para>
/// <para>
/// Oracle was the only backend without this. <c>ContainerCollections</c> already serialises Postgres,
/// SqlServer, Redis, MongoDB, Kafka, RabbitMQ, Elasticsearch, AWS SQS and Azure Service Bus; the absence of
/// an Oracle entry was the whole defect. The definition lives here rather than in <c>Tests.Shared</c>
/// because the fixture it binds is local to this assembly.
/// </para>
/// <para>
/// <b>Consequence for tests joining this collection:</b> the container is now shared across classes rather
/// than fresh per class, so a class MUST NOT assume a pristine schema. Reset what you depend on — the two
/// classes that already called <c>RequireDockerAndFreshSchemaAsync</c> were correct to, and that is now the
/// rule for everyone here rather than an option.
/// </para>
/// </remarks>
[CollectionDefinition(OracleOutboxCollection.Name)]
public sealed class OracleOutboxCollection : ICollectionFixture<OracleOutboxStoreContainerFixture>
{
	/// <summary>The collection name shared by every Oracle-backed class in this assembly.</summary>
	public const string Name = "Oracle Outbox";
}
