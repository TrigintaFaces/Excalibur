// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
namespace Excalibur.Integration.Tests.Redis;

/// <summary>
/// xUnit collection definition for Redis integration tests.
/// Collection definitions must be in the same assembly as the tests.
/// </summary>
[CollectionDefinition(CollectionName)]
public class RedisTestCollection : ICollectionFixture<RedisContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "Redis Integration Tests";
}
