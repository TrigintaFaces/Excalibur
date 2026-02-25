// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceConfigurationValidatorShould
{
	[Fact]
	public async Task StartAsync_ValidatesConfiguration()
	{
		var config = new PersistenceConfiguration();
		config.Providers["test"] = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "Mode=InMemory"
		};
		var logger = NullLogger<PersistenceConfigurationValidator>.Instance;
		var validator = new PersistenceConfigurationValidator(config, logger);

		await Should.NotThrowAsync(() => validator.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StopAsync_CompletesImmediately()
	{
		var config = new PersistenceConfiguration();
		var logger = NullLogger<PersistenceConfigurationValidator>.Instance;
		var validator = new PersistenceConfigurationValidator(config, logger);

		await Should.NotThrowAsync(() => validator.StopAsync(CancellationToken.None));
	}

	[Fact]
	public void ThrowForNullLogger()
	{
		var config = new PersistenceConfiguration();
		Should.Throw<ArgumentNullException>(
			() => new PersistenceConfigurationValidator(config, null!));
	}

	[Fact]
	public void ThrowForNonPersistenceConfiguration()
	{
		var config = A.Fake<IPersistenceConfiguration>();
		var logger = NullLogger<PersistenceConfigurationValidator>.Instance;

		Should.Throw<ArgumentException>(
			() => new PersistenceConfigurationValidator(config, logger));
	}
}
