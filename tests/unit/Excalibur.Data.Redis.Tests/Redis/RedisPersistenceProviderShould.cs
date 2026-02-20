// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisPersistenceProviderShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		var logger = NullLogger<RedisPersistenceProvider>.Instance;

		Should.Throw<ArgumentNullException>(
			() => new RedisPersistenceProvider(null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisProviderOptions
		{
			ConnectionString = "localhost:6379"
		});

		Should.Throw<ArgumentNullException>(
			() => new RedisPersistenceProvider(options, null!));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisProviderOptions
		{
			ConnectionString = string.Empty
		});
		var logger = NullLogger<RedisPersistenceProvider>.Instance;

		Should.Throw<ArgumentException>(
			() => new RedisPersistenceProvider(options, logger));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsWhitespace()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisProviderOptions
		{
			ConnectionString = "   "
		});
		var logger = NullLogger<RedisPersistenceProvider>.Instance;

		Should.Throw<ArgumentException>(
			() => new RedisPersistenceProvider(options, logger));
	}
}
