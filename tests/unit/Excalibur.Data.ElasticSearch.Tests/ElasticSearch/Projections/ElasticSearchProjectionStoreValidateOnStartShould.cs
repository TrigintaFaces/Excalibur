// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Regression locks for the ElasticSearch projection-store options fail-fast wiring:
/// <see cref="IValidateOptions{TOptions}"/> registration + eager <c>ValidateOnStart</c> validation
/// of the per-projection named options instance. Reverting either the <c>TryAddEnumerable</c> validator
/// registration or the <c>.ValidateOnStart()</c> marker must turn one of these RED.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ElasticSearchProjectionStoreValidateOnStartShould
{
	private sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;
	}

	[Fact]
	public void RegisterValidateOptionsForProjectionStoreOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddElasticSearchProjectionStore<TestProjection>(o => o.NodeUri = "http://localhost:9200");

		// Assert — the cross-property validator is wired via TryAddEnumerable (RED if that registration reverts).
		services.ShouldContain(d =>
			d.ServiceType == typeof(IValidateOptions<ElasticSearchProjectionStoreOptions>));
	}

	[Fact]
	public async Task FailFastAtHostStart_WhenProjectionOptionsInvalid()
	{
		// Arrange — bad NodeUri; ValidateOnStart must surface it eagerly at host start, not at first store use.
		var builder = Host.CreateApplicationBuilder();
		_ = builder.Services.AddElasticSearchProjectionStore<TestProjection>(o => o.NodeUri = "not-a-uri");
		using var host = builder.Build();

		// Act & Assert — RED if `.ValidateOnStart()` is removed (StartAsync would no longer validate).
		var ex = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
		ex.Message.ShouldContain("not a valid URI");

		await host.StopAsync();
	}

	[Fact]
	public async Task StartCleanly_WhenProjectionOptionsValid()
	{
		// Arrange — valid config: the fail-fast test must not be vacuously passing.
		var builder = Host.CreateApplicationBuilder();
		_ = builder.Services.AddElasticSearchProjectionStore<TestProjection>(o => o.NodeUri = "http://localhost:9200");
		using var host = builder.Build();

		// Act & Assert
		await Should.NotThrowAsync(() => host.StartAsync());

		await host.StopAsync();
	}
}
