// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// Crypto-shredding refuses to start when no key-destruction ledger is registered, and starts normally when
/// one is.
/// </summary>
/// <remarks>
/// <para>
/// <b>This arm exists because the configuration it covers became untested.</b> Five test classes used to fail
/// on exactly this misconfiguration — a container with crypto-shredding and no erasure store, where
/// <c>FieldEncryptor</c> cannot be constructed — and each was fixed by registering a store. That was the right
/// fix for those classes and it removed the only detector the repository had: the failing configuration is
/// now reachable by a consumer and asserted by nothing. This file is that detector, written deliberately
/// rather than inherited as a side effect.
/// </para>
/// <para>
/// <b>The liveness arm is not symmetry for its own sake.</b> A validator that refused every composition would
/// satisfy the safety arm perfectly, and a validator that refused nothing would satisfy the liveness arm. Only
/// the pair pins the behaviour to the condition.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CryptoShreddingRefusesToStartWithNoLedgerShould
{
	/// <summary>
	/// SAFETY. Crypto-shredding with no erasure store refuses to start rather than failing per-request.
	/// </summary>
	/// <remarks>
	/// Asserted through <c>ValidateStartupGates</c> rather than by starting a host, because that is the path a
	/// consumer who builds a provider directly takes and it is the one the prerequisite-validator registration
	/// exists to cover. The hosted-service registration covers the host path; both descriptors are registered.
	/// </remarks>
	[Fact]
	public void RefuseToStartWhenNoLedgerIsRegistered()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddCryptoShredding();

		using var provider = services.BuildServiceProvider();

		var failure = Should.Throw<InvalidOperationException>(provider.ValidateStartupGates);

		failure.Message.ShouldContain(
			"IKeyDestructionLedger",
			Case.Sensitive,
			"the message must name the service that is missing, or a consumer cannot tell which registration "
			+ "the refusal is about");
	}

	/// <summary>
	/// SAFETY. The refusal names remedies this framework actually ships, and says why they are the remedy.
	/// </summary>
	/// <remarks>
	/// A fail-closed message naming a remedy that does not exist has already shipped here once, so this is a
	/// repeat risk rather than a hypothetical. Each name below is asserted against the real registration
	/// methods; the "why" clause is asserted because "register an erasure store" is baffling advice to a
	/// consumer who only wanted field encryption and never mentioned erasure.
	/// </remarks>
	[Fact]
	public void NameARemedyThatExistsAndSayWhyItIsTheRemedy()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddCryptoShredding();

		using var provider = services.BuildServiceProvider();

		var message = Should.Throw<InvalidOperationException>(provider.ValidateStartupGates).Message;

		foreach (var remedy in new[]
			{
				nameof(ErasureServiceCollectionExtensions.AddInMemoryErasureStore),
			})
		{
			message.ShouldContain(
				remedy,
				Case.Sensitive,
				$"the refusal must name {remedy}, which is a registration this framework ships");
		}

		message.ShouldContain("AddPostgresErasureStore", Case.Sensitive);
		message.ShouldContain("AddSqlServerErasureStore", Case.Sensitive);

		message.ShouldContain(
			"destruction record",
			Case.Insensitive,
			"the message must connect the remedy to the symptom -- a crypto-shredded field reports as erased "
			+ "only on the strength of a destruction record, and the erasure store is what holds it. Without "
			+ "that sentence the advice reads as a non-sequitur and sends the reader to the source.");
	}

	/// <summary>
	/// LIVENESS. The same composition WITH an erasure store starts, and the field encryptor resolves.
	/// </summary>
	/// <remarks>
	/// Without this arm a validator that refused every composition would pass the safety arms above. Resolving
	/// <see cref="IFieldEncryptor"/> is asserted as well as the gate passing, because the gate's whole purpose
	/// is to predict that resolution: a gate that passed while the resolve still failed would have moved the
	/// error without removing it.
	/// </remarks>
	[Fact]
	public void StartNormallyWhenAnErasureStoreSuppliesTheLedger()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
		services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
		services.AddCryptoShredding();
		services.AddInMemoryErasureStore();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(provider.ValidateStartupGates);

		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IFieldEncryptor>().ShouldNotBeNull(
			"the gate exists to predict this resolution; a gate that passes while the resolve fails has moved "
			+ "the error rather than removed it");
	}
}
