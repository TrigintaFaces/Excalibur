// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// xUnit collection definition for Postgres integration tests.
/// Collection definitions must be in the same assembly as the tests.
/// </summary>
[CollectionDefinition(CollectionName)]
public class PostgresTestCollection : ICollectionFixture<PostgresContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "Postgres Integration Tests";
}
