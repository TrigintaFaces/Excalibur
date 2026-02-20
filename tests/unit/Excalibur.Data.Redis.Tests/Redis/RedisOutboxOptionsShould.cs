// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Outbox;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisOutboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.3): Redis unit tests.
/// Tests verify outbox options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Redis")]
[Trait("Feature", "Outbox")]
public sealed class RedisOutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultConnectionString()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.ConnectionString.ShouldBe("localhost:6379");
	}

	[Fact]
	public void HaveDefaultDatabaseId()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.DatabaseId.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultKeyPrefix()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.KeyPrefix.ShouldBe("outbox");
	}

	[Fact]
	public void HaveSentMessageTtlSecondsAsSevenDays()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert - 7 days = 604800 seconds
		options.SentMessageTtlSeconds.ShouldBe(604800);
	}

	[Fact]
	public void HaveDefaultConnectTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.ConnectTimeoutMs.ShouldBe(5000);
	}

	[Fact]
	public void HaveDefaultSyncTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.SyncTimeoutMs.ShouldBe(5000);
	}

	[Fact]
	public void HaveAbortOnConnectFailDisabledByDefault()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.AbortOnConnectFail.ShouldBeFalse();
	}

	[Fact]
	public void HaveUseSslDisabledByDefault()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.UseSsl.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullPasswordByDefault()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions();

		// Assert
		options.Password.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
		{
			KeyPrefix = "my-outbox"
		};

		// Assert
		options.KeyPrefix.ShouldBe("my-outbox");
	}

	[Fact]
	public void AllowSettingSentMessageTtlSeconds()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions
		{
			SentMessageTtlSeconds = 3600
		};

		// Assert
		options.SentMessageTtlSeconds.ShouldBe(3600);
	}

	[Fact]
	public void AllowSettingConnectTimeoutMs()
	{
		// Arrange & Act
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
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
		var options = new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379",
			KeyPrefix = "outbox"
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WithDefaultOptions()
	{
		// Arrange
		var options = new RedisOutboxOptions();

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(RedisOutboxOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(RedisOutboxOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
