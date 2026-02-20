// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Migration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MigrationServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddEventSourcingMigration());
	}

	[Fact]
	public void RegisterMigrationRunnerOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddEventSourcingMigration();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MigrationRunnerOptions>>();
		options.Value.ShouldNotBeNull();
		options.Value.ParallelStreams.ShouldBe(1);
		options.Value.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void RegisterMigrationOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddEventSourcingMigration();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MigrationOptions>>();
		options.Value.ShouldNotBeNull();
		options.Value.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void ApplyConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddEventSourcingMigration(opts =>
		{
			opts.ParallelStreams = 4;
			opts.DryRun = true;
			opts.BatchSize = 250;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MigrationRunnerOptions>>();
		options.Value.ParallelStreams.ShouldBe(4);
		options.Value.DryRun.ShouldBeTrue();
		options.Value.BatchSize.ShouldBe(250);
	}

	[Fact]
	public void RegisterWithoutConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddEventSourcingMigration();

		// Assert - defaults should be used
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MigrationRunnerOptions>>();
		options.Value.DryRun.ShouldBeFalse();
		options.Value.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddEventSourcingMigration();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
