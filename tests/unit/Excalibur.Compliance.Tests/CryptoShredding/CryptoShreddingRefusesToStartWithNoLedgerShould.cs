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

		// A pepper, because this arm is about the LEDGER. Crypto-shredding also validates its hashing pepper at
		// start-up, and a composition missing both would be refused for whichever gate ran first -- which would
		// let the ledger assertion below pass on the strength of an unrelated refusal.
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
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

		// A pepper, because this arm is about the LEDGER. Crypto-shredding also validates its hashing pepper at
		// start-up, and a composition missing both would be refused for whichever gate ran first -- which would
		// let the ledger assertion below pass on the strength of an unrelated refusal.
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
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

	/// <summary>
	/// SAFETY. The refusal offers the non-erasing deployment its own remedy, not only an erasure store.
	/// </summary>
	/// <remarks>
	/// A deployment that encrypts personal data at rest and never destroys a subject key reaches this refusal
	/// too, and for it "register an erasure store" is the wrong advice -- it would be adding a subsystem it has
	/// no use for to satisfy a message. The refusal has to name the other door or that reader is stuck.
	/// </remarks>
	[Fact]
	public void OfferTheNonErasingDeploymentItsOwnRemedy()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		// A pepper, because this arm is about the LEDGER. Crypto-shredding also validates its hashing pepper at
		// start-up, and a composition missing both would be refused for whichever gate ran first -- which would
		// let the ledger assertion below pass on the strength of an unrelated refusal.
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
		services.AddCryptoShredding();

		using var provider = services.BuildServiceProvider();

		var message = Should.Throw<InvalidOperationException>(provider.ValidateStartupGates).Message;

		message.ShouldContain(
			nameof(CryptoShreddingServiceCollectionExtensions.AddCryptoShreddingWithoutErasure),
			Case.Sensitive,
			"a deployment that encrypts at rest and never erases has no use for an erasure store, so the "
			+ "refusal must name the registration that fits it");
	}

	/// <summary>
	/// LIVENESS. The non-erasing opt-out starts on its own, and the field encryptor resolves.
	/// </summary>
	/// <remarks>
	/// The opt-out's whole purpose is to make this composition startable. Without this arm the opt-out could be
	/// registered and still refused, and both safety arms above would stay green.
	/// </remarks>
	[Fact]
	public void StartNormallyWhenTheNonErasingOptOutSuppliesTheLedger()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
		services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
		services.AddCryptoShreddingWithoutErasure();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(provider.ValidateStartupGates);

		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IFieldEncryptor>().ShouldNotBeNull(
			"the opt-out exists so that this composition can be built and used, not merely registered");
	}

	/// <summary>
	/// SAFETY. The non-erasing ledger REFUSES a destruction assertion rather than discarding it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm existed nowhere until an audit went looking for it, and its absence was the worst kind.</b>
	/// The no-erasure ledger's read answers <see langword="false" /> for every generation, which is the true
	/// answer in a deployment that destroys nothing. Its WRITE is the asymmetric half: a caller asserting a
	/// destruction is telling the framework that material WAS destroyed, in a composition declared to perform
	/// no erasure. There is nowhere durable to put that row, and a destruction that is not recorded cannot be
	/// reported as an erasure.
	/// </para>
	/// <para>
	/// So the only two alternatives to throwing are both silent: discard a compliance assertion the caller
	/// owns, or hold the row somewhere nothing persists and lose it at the next restart. Had this degraded to
	/// a completed task, no arm anywhere would have reddened, and a consumer would believe a destruction was
	/// recorded while nothing had been. The message is asserted too, because a refusal that does not name the
	/// three registrations which WOULD persist the row sends the reader to the source.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RefuseToRecordADestruction_WhenTheDeploymentDeclaredItPerformsNoErasure()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
		services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
		services.AddCryptoShreddingWithoutErasure();

		using var provider = services.BuildServiceProvider();
		var ledger = provider.GetRequiredService<IKeyDestructionLedger>();

		// LIVENESS FIRST: the read must still answer, or the arm below would pass against a ledger that
		// refuses everything -- which is the failure mode this suite's own remarks warn about.
		(await ledger.IsGenerationDestroyedAsync("handle-a", "0123456789abcdef0123456789abcdef", TestContext.Current.CancellationToken))
			.ShouldBeFalse("a deployment that destroys nothing has destroyed this generation too");

		var refusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await ledger.RecordDestroyedGenerationAsync(
				"handle-a", "0123456789abcdef0123456789abcdef", TestContext.Current.CancellationToken));

		foreach (var remedy in new[] { "AddInMemoryErasureStore", "AddPostgresErasureStore", "AddSqlServerErasureStore" })
		{
			refusal.Message.ShouldContain(
				remedy,
				Case.Sensitive,
				$"the refusal must name {remedy}, which WOULD persist the row the caller is asserting");
		}
	}

	/// <summary>
	/// SAFETY. The opt-out registered BESIDE an erasure store is refused, in either registration order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every ledger registration in this framework is a <c>TryAdd</c>, so with both calls present the winner is
	/// whichever ran first. One of the two orders puts an always-false ledger in front of a real erasure store:
	/// the deployment performs erasures, nothing ever tombstones, every erased subject reads back in the clear,
	/// and no component reports a problem. That is the silent direction, so it cannot be left to order.
	/// </para>
	/// <para>
	/// <b>Both orders are asserted deliberately.</b> Only one of them is dangerous, and a guard that caught only
	/// the dangerous one would be correct today and would silently stop being correct the moment a registration
	/// changed which call wins. Refusing the combination regardless of order is the property; catching the
	/// currently-harmful ordering is not.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RefuseTheNonErasingOptOutBesideAnErasureStore(bool optOutFirst)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");
		services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));

		if (optOutFirst)
		{
			services.AddCryptoShreddingWithoutErasure();
			services.AddInMemoryErasureStore();
		}
		else
		{
			services.AddInMemoryErasureStore();
			services.AddCryptoShreddingWithoutErasure();
		}

		using var provider = services.BuildServiceProvider();

		var message = Should.Throw<InvalidOperationException>(provider.ValidateStartupGates).Message;

		message.ShouldContain(
			nameof(CryptoShreddingServiceCollectionExtensions.AddCryptoShreddingWithoutErasure),
			Case.Sensitive,
			"the refusal must name the call to remove, or the consumer has to guess which of the two is wrong");
	}
}
