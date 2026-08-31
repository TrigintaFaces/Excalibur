// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.SqlServer;

/// <summary>
/// Binds the startup refusal that stops key escrow from running without an encryption provider.
/// </summary>
/// <remarks>
/// Escrow encrypts every key before it writes it, so a registration with no encryption provider is
/// not merely incomplete — it cannot work. Without this refusal the host starts, escrow appears to be
/// configured, and the fault surfaces at recovery: the one moment the feature exists for, and the one
/// moment when the fallback is that the key is gone. Failing at startup is the better outcome.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class KeyEscrowEncryptionWiringValidatorShould
{
	[Fact]
	public void Refuse_escrow_registered_without_an_encryption_provider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddSqlServerKeyEscrow(o => o.ConnectionString = "Server=test;Database=test;");

		using var sp = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => _ = sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);

		// The message has to say what is missing and what to do, or the operator reading it at 3am
		// learns only that something is wrong with an options object.
		ex.Message.ShouldContain("IEncryptionProvider");
	}

	[Fact]
	public void Allow_escrow_registered_with_an_encryption_provider()
	{
		// The liveness arm, and the one that matters most here: a guard that refused every
		// configuration would satisfy the arm above forever while making escrow unusable. Nothing else
		// in the suite would notice.
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddSqlServerKeyEscrow(o => o.ConnectionString = "Server=test;Database=test;");

		using var sp = services.BuildServiceProvider();

		var options = Should.NotThrow(
			() => sp.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>().Value);

		options.TableName.ShouldBe("KeyEscrow");

		// And the service itself still resolves — the guard must not have displaced the registration.
		_ = sp.GetRequiredService<IKeyEscrowService>().ShouldNotBeNull();
	}

	[Fact]
	public void Register_the_same_validators_from_the_configuration_overload()
	{
		// The two overloads register independently, so covering only the delegate one leaves the other
		// free to drift into accepting an unwired escrow. This asserts the wiring validator is present
		// on the configuration path too, without needing a configuration provider to bind from.
		var delegateServices = new ServiceCollection();
		delegateServices.AddLogging();
		_ = delegateServices.AddSqlServerKeyEscrow(o => o.ConnectionString = "Server=test;Database=test;");

		var configurationServices = new ServiceCollection();
		configurationServices.AddLogging();
		_ = configurationServices.AddSqlServerKeyEscrow(A.Fake<IConfiguration>());

		var delegateValidators = delegateServices
			.Count(d => d.ServiceType == typeof(IValidateOptions<SqlServerKeyEscrowOptions>));
		var configurationValidators = configurationServices
			.Count(d => d.ServiceType == typeof(IValidateOptions<SqlServerKeyEscrowOptions>));

		// Two: the options-shape validator and the encryption-wiring validator.
		delegateValidators.ShouldBe(2);
		configurationValidators.ShouldBe(delegateValidators);
	}
}
