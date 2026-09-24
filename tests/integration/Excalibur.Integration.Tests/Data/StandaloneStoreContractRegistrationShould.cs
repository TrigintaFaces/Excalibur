// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox.ElasticSearch;
using Excalibur.Outbox.Marten;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Every standalone store entry point registers the CONTRACT it is named for, not only keyed views of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT, AND WHY IT IS A POPULATION RATHER THAN A BUG.</b> These extensions build their store
/// through <c>AddTenantAwareStore&lt;TContract, TStore&gt;</c>, whose registration infers its service type
/// from the FACTORY's return type — which is the concrete store, never <c>TContract</c>. Each extension
/// then added keyed views by hand and none added the unkeyed one, so a host calling only
/// <c>AddXStore(...)</c> got a store it could not inject by its interface. The failure lands at startup,
/// after wiring, where no documentation reaches the consumer.
/// </para>
/// <para>
/// <b>WHY THE COMPOSITION PATH HID IT.</b> A consumer going through <c>AddExcaliburInbox</c> /
/// <c>AddExcaliburOutbox</c> is fine — those register the alias themselves. So the defect is invisible to
/// anyone following the documented composition, and reachable only by the consumer who wires one store
/// directly. It was originally filed against the two in-memory packages; those were fixed first and the
/// rest of the population was missed, which is what this arm exists to stop recurring.
/// </para>
/// <para>
/// <b>ORACLE IS COVERED SEPARATELY, NOT OMITTED.</b> This project does not reference
/// <c>Excalibur.Inbox.Oracle</c>, so <c>AddOracleInboxStore</c> is bound by an equivalent arm in that
/// package's own unit suite. Stated here so the four below are not read as the whole population.
/// </para>
/// <para>
/// <b>NO INFRASTRUCTURE IS TOUCHED.</b> These arms assert the SHAPE OF THE REGISTRATION on an
/// <see cref="IServiceCollection"/> and never build a provider or open a connection, so they carry no
/// dependency on SQL Server, Oracle, Elasticsearch or Marten being reachable. That is deliberate: the
/// defect is a registration defect, and binding it to real infrastructure would make the lock skip in
/// exactly the environments that most need it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class StandaloneStoreContractRegistrationShould
{
	/// <summary>
	/// The entry points under test, keyed by the extension-method name each arm is really identified by.
	/// </summary>
	/// <remarks>
	/// The delegate and the contract type live HERE rather than in the theory data, and the arms below take
	/// only the name. A theory argument must be serializable for the runner to identify a case on its own —
	/// an <see cref="Action{T}"/> and a <see cref="Type"/> are not, so passing them makes every case
	/// indistinguishable to the runner and unrunnable in isolation. The name is the natural key anyway: it is
	/// what the failure messages already quote.
	/// </remarks>
	private static readonly Dictionary<string, (Action<IServiceCollection> Register, Type Contract)> EntryPoints =
		new(StringComparer.Ordinal)
		{
			["AddSqlServerInboxStore"] =
				(s => s.AddSqlServerInboxStore(o => o.ConnectionString = "Server=.;Database=d;Integrated Security=true"), typeof(IInboxStore)),
			["AddElasticsearchOutboxStore"] =
				(s => s.AddElasticsearchOutboxStore(o => o.IndexName = "excalibur-outbox-test"), typeof(IOutboxStore)),
			["AddMartenOutboxStore"] =
				(s => s.AddMartenOutboxStore(), typeof(IOutboxStore)),
			["AddSqlServerOutboxStore"] =
				(s => s.AddSqlServerOutboxStore(o => o.ConnectionString = "Server=.;Database=d;Integrated Security=true"), typeof(IOutboxStore)),
		};

	public static TheoryData<string> StandaloneEntryPoints() => [.. EntryPoints.Keys];

	/// <summary>
	/// SAFETY. The contract the method is named for must be registered unkeyed.
	/// </summary>
	[Theory]
	[MemberData(nameof(StandaloneEntryPoints))]
	public void RegisterTheNonKeyedContract(string name)
	{
		var (register, contract) = EntryPoints[name];

		var services = new ServiceCollection();

		register(services);

		services.Count(d => d.ServiceType == contract && !d.IsKeyedService).ShouldBe(
			1,
			$"{name} must register {contract.Name} unkeyed. Zero means a host that calls only this "
			+ "extension cannot inject the contract the method is named for, and meets that at startup "
			+ "rather than at wiring");
	}

	/// <summary>
	/// SAFETY. Calling twice must not accumulate aliases — these use TryAdd semantics.
	/// </summary>
	[Theory]
	[MemberData(nameof(StandaloneEntryPoints))]
	public void RegisterTheNonKeyedContractExactlyOnce_WhenCalledTwice(string name)
	{
		var (register, contract) = EntryPoints[name];

		var services = new ServiceCollection();

		register(services);
		register(services);

		services.Count(d => d.ServiceType == contract && !d.IsKeyedService).ShouldBe(
			1,
			$"{name} must use TryAdd for the alias, so a second call leaves one descriptor rather than "
			+ "shadowing the first");
	}

	/// <summary>
	/// LIVENESS, and without it the arms above are satisfied by an extension that registers the alias and
	/// nothing else. The keyed views these stores are actually resolved through must survive.
	/// </summary>
	[Theory]
	[MemberData(nameof(StandaloneEntryPoints))]
	public void StillRegisterTheKeyedDefaultView(string name)
	{
		var (register, contract) = EntryPoints[name];

		var services = new ServiceCollection();

		register(services);

		services.Count(d => d.ServiceType == contract && d.IsKeyedService && (d.ServiceKey as string) == "default")
			.ShouldBe(
				1,
				$"{name} must keep its keyed \"default\" registration — the alias FORWARDS to it, so "
				+ "removing it would replace one resolution failure with another");
	}
}
