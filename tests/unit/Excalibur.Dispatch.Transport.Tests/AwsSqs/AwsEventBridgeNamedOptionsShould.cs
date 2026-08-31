// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// The AWS EventBridge transport is registered under a name while its runtime options were registered
/// without one, so two named EventBridge transports in one container wrote the same options instance and
/// the second silently replaced the first -- the losing transport published to the winner's event bus.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AwsEventBridgeNamedOptionsShould
{
	private static AwsEventBridgeOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<AwsEventBridgeOptions>>().Get(name);

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsEventBridgeTransport("orders", bus => bus
			.EventBusName("orders-bus")
			.DefaultSource("orders.service")
			.Region("us-east-1"));

		_ = services.AddAwsEventBridgeTransport("audit", bus => bus
			.EventBusName("audit-bus")
			.DefaultSource("audit.service")
			.Region("us-west-2"));

		using var provider = services.BuildServiceProvider();

		// Pre-fix both names read the SECOND registration's values.
		Resolve(provider, "orders").EventBusName.ShouldBe("orders-bus");
		Resolve(provider, "orders").DefaultSource.ShouldBe("orders.service");

		Resolve(provider, "audit").EventBusName.ShouldBe("audit-bus");
		Resolve(provider, "audit").DefaultSource.ShouldBe("audit.service");
	}

	[Fact]
	public void StillConfigureTheUnnamedOptionsForASingleTransportHost()
	{
		// Liveness. Naming the options and stopping there leaves IOptions<AwsEventBridgeOptions> resolving
		// an empty object, which is a quieter failure than the overwrite being fixed.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsEventBridgeTransport(bus => bus.EventBusName("single-bus"));

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<AwsEventBridgeOptions>>().Value.EventBusName.ShouldBe("single-bus");
		Resolve(provider, "aws-eventbridge").EventBusName.ShouldBe("single-bus");
	}

	[Fact]
	public void CarryEverySourcePropertyToTheResolvedOptionsOrDeclareWhyNot()
	{
		// The dropped-property arm. AwsEventBridgeTransportOptions is translated into
		// AwsEventBridgeOptions by a hand-written copy, so a property added to the source silently fails
		// to arrive unless somebody extends that copy. The source properties are enumerated by
		// reflection; one with no entry below FAILS rather than being skipped.
		var expectations = new Dictionary<string, Action<AwsEventBridgeTransportOptions, AwsEventBridgeOptions>>(StringComparer.Ordinal)
		{
			["EventBusName"] = (src, o) => o.EventBusName.ShouldBe(src.EventBusName),
			["DefaultSource"] = (src, o) => o.DefaultSource.ShouldBe(src.DefaultSource),
			["DefaultDetailType"] = (src, o) => o.DefaultDetailType.ShouldBe(src.DefaultDetailType),
			["EnableArchiving"] = (src, o) => o.EnableArchiving.ShouldBe(src.EnableArchiving),
			["ArchiveName"] = (src, o) => o.ArchiveName.ShouldBe(src.ArchiveName),
			["ArchiveRetentionDays"] = (src, o) => o.ArchiveRetentionDays.ShouldBe(src.ArchiveRetentionDays),

			// Deliberately not mapped, reason recorded so "unmapped" is a decision rather than an omission.
			["Name"] = static (_, _) => { }, // the DI registration name, not a runtime option
			["Region"] = static (_, _) => { }, // consumed when constructing AmazonEventBridgeClient
		};

		var settable = typeof(AwsEventBridgeTransportOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanWrite)
			.ToList();

		settable.ShouldNotBeEmpty();

		var undeclared = settable.Select(p => p.Name).Where(n => !expectations.ContainsKey(n)).ToList();
		undeclared.ShouldBeEmpty(
			$"AwsEventBridgeTransportOptions gained {string.Join(", ", undeclared)}. Map it in RegisterOptions " +
			"and add an expectation here, or record here why it is deliberately not mapped.");

		var configured = new AwsEventBridgeTransportOptions();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAwsEventBridgeTransport("coverage", bus => bus.ConfigureOptions(o =>
		{
			// Non-default throughout, so no assertion can pass by both sides holding the default.
			o.EventBusName = "coverage-bus";
			o.DefaultSource = "coverage.source";
			o.DefaultDetailType = "coverage.detail";
			o.EnableArchiving = true;
			o.ArchiveName = "coverage-archive";
			o.ArchiveRetentionDays = 41;

			foreach (var property in settable)
			{
				property.SetValue(configured, property.GetValue(o));
			}
		}));

		using var provider = services.BuildServiceProvider();
		var resolved = Resolve(provider, "coverage");

		foreach (var property in settable)
		{
			expectations[property.Name](configured, resolved);
		}
	}
}
