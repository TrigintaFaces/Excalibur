// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Inbox;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisInboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.3): Redis unit tests.
/// Tests verify inbox options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Redis")]
[Trait("Feature", "Inbox")]
public sealed class RedisInboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultConnectionString()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.ConnectionString.ShouldBe("localhost:6379");
	}

	[Fact]
	public void HaveDefaultDatabaseId()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.DatabaseId.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultKeyPrefix()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.KeyPrefix.ShouldBe("inbox");
	}

	[Fact]
	public void HaveDefaultTtlSecondsAsSevenDays()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert - 7 days = 604800 seconds
		options.DefaultTtlSeconds.ShouldBe(604800);
	}

	[Fact]
	public void HaveDefaultConnectTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.ConnectTimeoutMs.ShouldBe(5000);
	}

	[Fact]
	public void HaveDefaultSyncTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.SyncTimeoutMs.ShouldBe(5000);
	}

	[Fact]
	public void HaveAbortOnConnectFailDisabledByDefault()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.AbortOnConnectFail.ShouldBeFalse();
	}

	[Fact]
	public void HaveUseSslDisabledByDefault()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.UseSsl.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullPasswordByDefault()
	{
		// Arrange & Act
		var options = new RedisInboxOptions();

		// Assert
		options.Password.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			ConnectionString = "redis.example.com:6379"
		};

		// Assert
		options.ConnectionString.ShouldBe("redis.example.com:6379");
	}

	[Fact]
	public void AllowSettingDatabaseId()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			DatabaseId = 5
		};

		// Assert
		options.DatabaseId.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingKeyPrefix()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			KeyPrefix = "my-inbox"
		};

		// Assert
		options.KeyPrefix.ShouldBe("my-inbox");
	}

	[Fact]
	public void AllowSettingDefaultTtlSeconds()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			DefaultTtlSeconds = 3600
		};

		// Assert
		options.DefaultTtlSeconds.ShouldBe(3600);
	}

	[Fact]
	public void AllowSettingConnectTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			ConnectTimeoutMs = 10000
		};

		// Assert
		options.ConnectTimeoutMs.ShouldBe(10000);
	}

	[Fact]
	public void AllowSettingSyncTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			SyncTimeoutMs = 10000
		};

		// Assert
		options.SyncTimeoutMs.ShouldBe(10000);
	}

	[Fact]
	public void AllowSettingAbortOnConnectFail()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			AbortOnConnectFail = true
		};

		// Assert
		options.AbortOnConnectFail.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingUseSsl()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			UseSsl = true
		};

		// Assert
		options.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingPassword()
	{
		// Arrange & Act
		var options = new RedisInboxOptions
		{
			Password = "secret-password"
		};

		// Assert
		options.Password.ShouldBe("secret-password");
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenConnectionStringIsNull()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			ConnectionString = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			ConnectionString = string.Empty
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenConnectionStringIsWhitespace()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			ConnectionString = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenKeyPrefixIsNull()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			KeyPrefix = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenKeyPrefixIsEmpty()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			KeyPrefix = string.Empty
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenKeyPrefixIsWhitespace()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			KeyPrefix = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenValidOptions()
	{
		// Arrange
		var options = new RedisInboxOptions
		{
			ConnectionString = "localhost:6379",
			KeyPrefix = "inbox"
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WithDefaultOptions()
	{
		// Arrange
		var options = new RedisInboxOptions();

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(RedisInboxOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(RedisInboxOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
