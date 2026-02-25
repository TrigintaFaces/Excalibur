// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

[CollectionDefinition(nameof(ElasticsearchHostTests), DisableParallelization = true)]
public class ElasticsearchHostTests : ICollectionFixture<ElasticsearchContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}
