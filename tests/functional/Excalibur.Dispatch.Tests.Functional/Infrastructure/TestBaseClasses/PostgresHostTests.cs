// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure.TestBaseClasses;

[CollectionDefinition(nameof(PostgresHostTests))]
public class PostgresHostTests : ICollectionFixture<PostgresContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}
