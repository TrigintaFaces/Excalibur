// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure.TestBaseClasses;

[CollectionDefinition(nameof(SqlServerPersistenceOnlyTests))]
[Trait("Category", "Functional")]
[Trait("Component", "Core")]
public sealed class SqlServerPersistenceOnlyTests : ICollectionFixture<SqlServerContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}
