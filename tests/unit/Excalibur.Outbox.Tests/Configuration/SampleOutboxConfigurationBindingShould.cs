// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Helpers;

namespace Excalibur.Outbox.Tests.Configuration;

/// <summary>
/// Binds the outbox section of the shipped sample <c>appsettings.json</c> files and asserts the values
/// reach <see cref="SqlServerOutboxOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// A sample that binds a section or key the options type does not declare does not fail. The section
/// does not resolve, nothing binds, and the options keep their defaults — so a consumer who copies the
/// sample and changes a value gets no effect and no error. We accept an instruction and discard it,
/// which is worse than omitting the setting because the consumer has explicit evidence it was accepted.
/// </para>
/// <para>
/// Both samples set values that are identical to the defaults, which is exactly why the previous
/// wrong key path survived: asserting the bound value equals "dbo" passes whether binding happened or
/// not. These arms therefore seed every target property with a sentinel FIRST and assert the sample's
/// value replaced it. The assertion fails if the section path, the nesting, or any key name drifts
/// from what the options type declares.
/// </para>
/// </remarks>
public sealed class SampleOutboxConfigurationBindingShould
{
	private const string BindConfigurationPatterns =
		"samples/01-getting-started/BindConfigurationPatterns/appsettings.json";

	private const string FullStackAddExcalibur =
		"samples/11-real-world/FullStackAddExcalibur/appsettings.json";

	// The section path each sample documents, and the one its Program.cs passes to BindConfiguration.
	private const string OutboxSection = "Outbox:Sql";

	[Theory]
	[InlineData(BindConfigurationPatterns)]
	[InlineData(FullStackAddExcalibur)]
	public void BindTheSampleOutboxSectionOntoTheRealOptionsType(string sampleRelativePath)
	{
		var options = SeededWithSentinels();

		SectionOf(sampleRelativePath).Bind(options);

		// Table naming lives on the nested Tables object. The store reads Tables.*; the top-level
		// SchemaName/OutboxTableName on SqlServerOutboxOptions are read by nothing.
		options.Tables.SchemaName.ShouldBe("dbo");
		options.Tables.OutboxTableName.ShouldBe("OutboxMessages");

		// Processing settings live on the nested Processing object.
		options.Processing.DefaultBatchSize.ShouldBe(100);
		options.Processing.MaxRetryCount.ShouldBe(5);
		options.Processing.RetryDelayMinutes.ShouldBe(5);
	}

	[Theory]
	[InlineData(BindConfigurationPatterns)]
	[InlineData(FullStackAddExcalibur)]
	public void LeaveTheSentinelsUntouchedWhenTheSectionPathIsWrong(string sampleRelativePath)
	{
		// Liveness arm: proves the arm above is non-vacuous. A section the sample does not declare
		// binds nothing, so every sentinel survives. If this ever passes with the real path, or the
		// arm above passes with this one, binding is not what the assertions are measuring.
		var options = SeededWithSentinels();

		ConfigurationOf(sampleRelativePath).GetSection("Outbox:SqlServer").Bind(options);

		options.Tables.SchemaName.ShouldBe("sentinel-schema");
		options.Tables.OutboxTableName.ShouldBe("sentinel-table");
		options.Processing.DefaultBatchSize.ShouldBe(-1);
	}

	private static SqlServerOutboxOptions SeededWithSentinels()
	{
		var options = new SqlServerOutboxOptions();
		options.Tables.SchemaName = "sentinel-schema";
		options.Tables.OutboxTableName = "sentinel-table";
		options.Processing.DefaultBatchSize = -1;
		options.Processing.MaxRetryCount = -1;
		options.Processing.RetryDelayMinutes = -1;
		return options;
	}

	private static IConfigurationSection SectionOf(string sampleRelativePath) =>
		ConfigurationOf(sampleRelativePath).GetSection(OutboxSection);

	private static IConfigurationRoot ConfigurationOf(string sampleRelativePath) =>
		new ConfigurationBuilder()
			.AddJsonFile(ShippedSchemaScript.Resolve(sampleRelativePath), optional: false)
			.Build();
}
