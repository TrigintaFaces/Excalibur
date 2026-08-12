// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests;

/// <summary>
/// Locks the contract of <c>ConfigureApplicationContext</c>: a host that says nothing about its
/// application context still starts, and anything it does say wins.
/// </summary>
/// <remarks>
/// <para>
/// The defect these lock against: the defaults were applied to a detached dictionary read out of
/// configuration, while the options were bound from configuration itself. The static context got a
/// usable value and the options got an empty one, and the validator -- looking only at the options
/// -- failed the host for the absence of a value the same method had just computed.
/// </para>
/// <para>
/// The liveness arm is the one that matters here, and it is the one that would be forgotten. A
/// validator can be made to pass by requiring the consumer to supply everything; what has to be
/// proven is that a host with NO configuration section still starts. The final test is the
/// structural one: the two paths must agree, because the defect was never a wrong default, it was
/// two defaults maintained separately.
/// </para>
/// </remarks>
public sealed class ConfigureApplicationContextShould
{
	private const string SectionName = "ApplicationContext";

	private static HostApplicationBuilder BuilderWith(params (string Key, string Value)[] settings)
	{
		var builder = Host.CreateApplicationBuilder();
		if (settings.Length > 0)
		{
			_ = builder.Configuration.AddInMemoryCollection(
				settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
		}

		return builder;
	}

	[Fact]
	public void StartAHostThatConfiguresNoApplicationContextSection()
	{
		// The reported failure: OptionsValidationException naming both fields, on a host that
		// simply had not written the section. Building the provider and resolving the options is
		// what ValidateOnStart ultimately does.
		var builder = BuilderWith();

		_ = builder.ConfigureApplicationContext();

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value;

		options.ApplicationName.ShouldNotBeNullOrWhiteSpace();
		options.ApplicationSystemName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void PassTheValidatorWithNoConfigurationSection()
	{
		// Asserting the validator's own verdict rather than only the values, because the validator
		// is what refused the host.
		var builder = BuilderWith();
		_ = builder.ConfigureApplicationContext();

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value;

		var validator = new ApplicationContextOptionsValidator();
		validator.Validate(name: null, options).Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PreferConfiguredValuesOverTheDefaults()
	{
		// Safety: a default must never overwrite something the consumer actually said.
		var builder = BuilderWith(
			($"{SectionName}:ApplicationName", "Explicit App"),
			($"{SectionName}:ApplicationSystemName", "explicit-system"));

		_ = builder.ConfigureApplicationContext();

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value;

		options.ApplicationName.ShouldBe("Explicit App");
		options.ApplicationSystemName.ShouldBe("explicit-system");
	}

	[Fact]
	public void CompleteOnlyTheValueThatIsMissing()
	{
		// A partially configured section is the case most likely to regress: filling every field
		// unconditionally, or filling none when one is present, are both easy mistakes.
		var builder = BuilderWith(($"{SectionName}:ApplicationName", "Only The Name"));

		_ = builder.ConfigureApplicationContext();

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value;

		options.ApplicationName.ShouldBe("Only The Name");
		options.ApplicationSystemName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void GiveTheStaticContextAndTheOptionsTheSameValues()
	{
		// THE STRUCTURAL LOCK. The bug was not a wrong default; it was two defaults, applied to two
		// different objects, that happened to disagree. This asserts the property -- the two paths
		// carry the same values -- rather than the mechanism that currently provides it, so it
		// still fails if someone reintroduces a second source of defaults.
		var builder = BuilderWith();

		_ = builder.ConfigureApplicationContext();

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value;

		options.ApplicationName.ShouldBe(ApplicationContext.ApplicationName);
		options.ApplicationSystemName.ShouldBe(ApplicationContext.ApplicationSystemName);
	}
}
