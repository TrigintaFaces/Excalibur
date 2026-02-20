// Copyright (c) TrigintaFaces. All rights reserved.

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Data;

/// <summary>
/// xUnit test collection for SQL Server functional tests.
/// All tests in this collection share a single SQL Server container instance.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
	public const string CollectionName = "SqlServer Functional Tests";
}
