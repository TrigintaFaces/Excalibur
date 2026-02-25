// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// xUnit collection definition for SQL Server integration tests.
/// Collection definitions must be in the same assembly as the tests.
/// </summary>
[CollectionDefinition(CollectionName)]
public class SqlServerTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "SQL Server Integration Tests";
}
