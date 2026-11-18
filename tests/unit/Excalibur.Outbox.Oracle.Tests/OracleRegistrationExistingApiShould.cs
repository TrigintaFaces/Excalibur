// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>Current public API only: run before adopting the provider implementation.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OracleRegistrationExistingApiShould
{
	[Theory]
	[InlineData("direct")]
	[InlineData("named")]
	[InlineData("bound")]
	[InlineData("borrowed")]
	public async Task ResolveTheAdvertisedProviderModeWithoutOpeningAnUnrelatedGlobalDb(string mode)
	{
		const string unopened = "User Id=unit_only;Password=unit_only;Data Source=unit.invalid:1521/unit";
		var unrelated = A.Fake<IDb>();
		var selectedBorrowed = A.Fake<IDb>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(unrelated);
		services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:outbox"] = unopened,
			["Oracle:ConnectionString"] = unopened,
			["Oracle:OutboxTableName"] = "CUSTOM_OUTBOX",
		}).Build());
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseOracle(oracle =>
		{
			if (mode == "direct") { oracle.ConnectionString(unopened); }
			else if (mode == "named") { oracle.ConnectionStringName("outbox"); }
			else if (mode == "bound") { oracle.BindConfiguration("Oracle"); }
			else { oracle.ConnectionFactory(_ => selectedBorrowed); }
		})));
		await using var provider = services.BuildServiceProvider(validateScopes: true);
		var store = provider.GetRequiredService<IOutboxStore>();
		store.ShouldBeOfType<OracleOutboxStore>();
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("oracle"));
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("default"));
		A.CallTo(() => unrelated.Connection).MustNotHaveHappened();
		A.CallTo(() => selectedBorrowed.Connection).MustNotHaveHappened();
	}
}
