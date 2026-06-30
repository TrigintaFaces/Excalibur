// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Threading.Tasks;

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Configuration;

/// <summary>
/// Independent regression lock for the Microsoft-style <see cref="IComplianceEncryptionBuilder"/> surface.
/// The retired flat <c>AddComplianceEncryption(Action&lt;InMemoryKeyManagementOptions&gt;, Action&lt;AesGcmEncryptionOptions&gt;)</c>
/// paradigm was deleted greenfield (no <c>[Obsolete]</c> shim); the only entry point is now the single
/// <c>AddComplianceEncryption(Action&lt;IComplianceEncryptionBuilder&gt;)</c> overload. These tests assert the new builder
/// wiring is correct, that FIPS validation is an always-on default, that <c>ValidateOnStart</c> fail-fast is live, and
/// — via reflection — that the flat dual-<c>Action</c> overload no longer exists.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ComplianceEncryptionBuilderShould
{
	private static IHost BuildHost(Action<IServiceCollection> configureServices)
		=> new HostBuilder()
			.ConfigureServices((_, services) =>
			{
				services.AddLogging();
				configureServices(services);
			})
			.Build();

	[Fact]
	public void RegisterEncryptionKeyManagementRotationAndFips_ViaBuilder()
	{
		// Arrange & Act
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddComplianceEncryption(builder => builder
			.WithEncryption(o => o.DefaultPurpose = "pii")
			.WithInMemoryKeyManagement()
			.WithKeyRotation(o => o.MaxKeyAge = TimeSpan.FromDays(30)));

		// Assert — every collaborator the builder lambda requested is resolvable.
		using var provider = services.BuildServiceProvider();

		provider.GetService<IKeyManagementProvider>().ShouldNotBeNull();
		provider.GetService<IKeyManagementAdmin>().ShouldNotBeNull();

		var encryption = provider.GetService<IEncryptionProvider>();
		encryption.ShouldNotBeNull();
		// WithKeyRotation wraps the base provider — proves the rotation selection was honored.
		encryption.ShouldBeOfType<RotatingEncryptionProvider>();

		// FIPS validation is always wired.
		provider.GetService<IFipsDetector>().ShouldNotBeNull();
		provider.GetService<FipsValidationService>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterFipsValidation_ByDefault_WithoutAnyExplicitOptIn()
	{
		// Arrange & Act — no WithFips* call exists; FIPS must be on regardless.
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddComplianceEncryption(builder => builder.WithEncryption());

		// Assert
		using var provider = services.BuildServiceProvider();
		provider.GetService<IFipsDetector>().ShouldNotBeNull();
		provider.GetService<FipsValidationService>().ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowAtStartup_WhenEncryptionOptionsAreInvalid()
	{
		// A non-null whitespace DefaultPurpose is rejected by AesGcmEncryptionOptionsValidator.
		using var host = BuildHost(services => services.AddComplianceEncryption(builder =>
			builder.WithEncryption(o => o.DefaultPurpose = "   ")));

		await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
	}

	[Fact]
	public async Task StartCleanly_WhenOptionsAreValid()
	{
		// Valid defaults must still start — fail-fast is unconditional but not over-strict.
		using var host = BuildHost(services => services.AddComplianceEncryption(builder =>
			builder.WithEncryption().WithInMemoryKeyManagement().WithKeyRotation()));

		await host.StartAsync();
		await host.StopAsync();
	}

	[Fact]
	public void ExposeNoFlatDualActionOverload_OnlyTheBuilderOverload()
	{
		// Retirement guard: RED if the pre-refactor flat (Action, Action) paradigm is still present.
		var overloads = typeof(ComplianceServiceCollectionExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => string.Equals(m.Name, nameof(ComplianceServiceCollectionExtensions.AddComplianceEncryption), StringComparison.Ordinal))
			.ToArray();

		// No overload may take two-or-more Action<> delegate parameters (the flat dual-Action shape).
		var flat = overloads.Where(m => m.GetParameters().Count(IsActionParameter) >= 2).ToArray();
		flat.ShouldBeEmpty("The retired flat AddComplianceEncryption(Action, Action) overload must not exist.");

		// Exactly one builder overload: AddComplianceEncryption(this IServiceCollection, Action<IComplianceEncryptionBuilder>).
		var builderOverloads = overloads.Where(m =>
		{
			var parameters = m.GetParameters();
			return parameters.Length == 2
				&& parameters[1].ParameterType == typeof(Action<IComplianceEncryptionBuilder>);
		}).ToArray();

		builderOverloads.Length.ShouldBe(1);

		static bool IsActionParameter(ParameterInfo p) =>
			p.ParameterType.IsGenericType
			&& p.ParameterType.GetGenericTypeDefinition() == typeof(Action<>);
	}
}
