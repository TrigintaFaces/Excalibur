// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

/// <summary>
/// Shared Elasticsearch container for the security-audit test family.
/// </summary>
/// <remarks>
/// <para>
/// These tests get their own collection — and therefore their own container — rather than joining
/// <see cref="ElasticsearchHostTests"/>. The reason is not preference: the code under test
/// (<c>SecurityAuditMaintenanceService</c>) hardcodes the index pattern <c>security-audit-*</c>, so these
/// tests cannot be isolated by index prefix the way every other Elasticsearch test is. They must reset
/// that global pattern before each fact, and a reset of a global pattern must not be able to reach any
/// other test's data. A separate container is what makes that guarantee structural instead of a
/// convention someone has to remember.
/// </para>
/// <para>
/// Within this collection, isolation is by up-front reset — see <c>ElasticsearchAuditTestBase</c>.
/// </para>
/// </remarks>
[CollectionDefinition(nameof(ElasticsearchAuditTests), DisableParallelization = true)]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchAuditTests : ICollectionFixture<ElasticsearchContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}
