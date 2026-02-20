// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for RedisProviderOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedisProviderOptionsShould : UnitTestBase
{
	#region Default Values

	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new RedisProviderOptions();

		// Assert
		options.Name.ShouldBeNull();
		options.ConnectionString.ShouldBe(string.Empty);
		options.Password.ShouldBeNull();
		options.DatabaseId.ShouldBe(0);
		options.ConnectTimeout.ShouldBe(10);
		options.SyncTimeout.ShouldBe(5);
		options.AsyncTimeout.ShouldBe(5);
		options.ConnectRetry.ShouldBe(3);
		options.AbortOnConnectFail.ShouldBeFalse();
		options.AllowAdmin.ShouldBeFalse();
		options.UseSsl.ShouldBeFalse();
		options.RetryCount.ShouldBe(3);
		options.IsReadOnly.ShouldBeFalse();
	}

	#endregion Default Values

	#region Property Customization

	[Fact]
	public void AllowCustomName()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			Name = "custom-redis"
		};

		// Assert
		options.Name.ShouldBe("custom-redis");
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			ConnectionString = "localhost:6379,password=secret"
		};

		// Assert
		options.ConnectionString.ShouldBe("localhost:6379,password=secret");
	}

	[Fact]
	public void AllowCustomDatabaseId()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			DatabaseId = 5
		};

		// Assert
		options.DatabaseId.ShouldBe(5);
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			ConnectTimeout = 30,
			SyncTimeout = 15,
			AsyncTimeout = 20
		};

		// Assert
		options.ConnectTimeout.ShouldBe(30);
		options.SyncTimeout.ShouldBe(15);
		options.AsyncTimeout.ShouldBe(20);
	}

	[Fact]
	public void AllowCustomRetrySettings()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			ConnectRetry = 5,
			RetryCount = 10
		};

		// Assert
		options.ConnectRetry.ShouldBe(5);
		options.RetryCount.ShouldBe(10);
	}

	[Fact]
	public void AllowSslConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			UseSsl = true
		};

		// Assert
		options.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void AllowAdminConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			AllowAdmin = true
		};

		// Assert
		options.AllowAdmin.ShouldBeTrue();
	}

	[Fact]
	public void AllowAbortOnConnectFailConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			AbortOnConnectFail = true
		};

		// Assert
		options.AbortOnConnectFail.ShouldBeTrue();
	}

	[Fact]
	public void AllowReadOnlyConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			IsReadOnly = true
		};

		// Assert
		options.IsReadOnly.ShouldBeTrue();
	}

	[Fact]
	public void AllowPasswordConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			Password = "super-secret-password"
		};

		// Assert
		options.Password.ShouldBe("super-secret-password");
	}

	#endregion Property Customization

	#region Complex Configurations

	[Fact]
	public void SupportProductionConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			Name = "production-redis",
			ConnectionString = "redis.example.com:6380",
			Password = "prod-password",
			DatabaseId = 1,
			UseSsl = true,
			ConnectTimeout = 30,
			SyncTimeout = 10,
			AsyncTimeout = 10,
			ConnectRetry = 5,
			RetryCount = 5,
			AbortOnConnectFail = false,
			IsReadOnly = false
		};

		// Assert
		options.Name.ShouldBe("production-redis");
		options.ConnectionString.ShouldBe("redis.example.com:6380");
		options.Password.ShouldBe("prod-password");
		options.DatabaseId.ShouldBe(1);
		options.UseSsl.ShouldBeTrue();
		options.ConnectTimeout.ShouldBe(30);
		options.SyncTimeout.ShouldBe(10);
		options.AsyncTimeout.ShouldBe(10);
		options.ConnectRetry.ShouldBe(5);
		options.RetryCount.ShouldBe(5);
		options.AbortOnConnectFail.ShouldBeFalse();
		options.IsReadOnly.ShouldBeFalse();
	}

	[Fact]
	public void SupportReadReplicaConfiguration()
	{
		// Arrange & Act
		var options = new RedisProviderOptions
		{
			Name = "redis-replica",
			ConnectionString = "replica.redis.example.com:6379",
			IsReadOnly = true,
			AllowAdmin = false
		};

		// Assert
		options.Name.ShouldBe("redis-replica");
		options.IsReadOnly.ShouldBeTrue();
		options.AllowAdmin.ShouldBeFalse();
	}

	#endregion Complex Configurations
}
