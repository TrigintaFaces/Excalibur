// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// xUnit collection definition for SQL Server SagaStore integration tests.
/// </summary>
[CollectionDefinition("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreTestCollection : ICollectionFixture<SqlServerSagaStoreContainerFixture>
{
}
