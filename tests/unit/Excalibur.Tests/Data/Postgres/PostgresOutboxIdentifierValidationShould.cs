// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Data.Postgres;

/// <summary>
/// Locks every config-derived SQL identifier on the Postgres outbox store options to the allowlist, at the
/// wire level: a real <see cref="ServiceProvider"/> built through the production
/// <c>AddExcaliburOutbox(...).UsePostgres(...)</c> path, with validation triggered exactly as a host triggers
/// it — by materializing <c>IOptions&lt;PostgresOutboxStoreOptions&gt;.Value</c> (<c>ValidateOnStart</c>).
/// </summary>
/// <remarks>
/// <para>
/// The table names on this options type are interpolated into statement text by the <c>Qualified*TableName</c>
/// projections, so each is a SQL-injection surface that no parameter can cover — an identifier cannot be
/// parameterized. The allowlist is the only guard, and the double-quoting those projections apply is not a
/// substitute for it: a <c>"</c> in the value closes the quote.
/// </para>
/// <para>
/// <b>Why the exhaustiveness arm exists, and why it is the load-bearing one.</b> The schema, outbox and
/// dead-letter names were validated while the fence control table — added later, and interpolated by exactly
/// the same mechanism — was not. Nothing detected that, because "every interpolated identifier is validated"
/// was a convention re-applied by hand at each property rather than a property of the type. The arm below
/// enumerates the identifiers off the options type itself, so a table name added tomorrow is covered on the
/// day it is added rather than on the day someone remembers. It asserts a non-zero enumeration first, so it
/// cannot pass by finding nothing to check.
/// </para>
/// <para>
/// LIVENESS: <see cref="ResolvesCleanly_WhenEveryIdentifierIsValid"/> proves the validator does not simply
/// reject everything — without it, a validator that failed unconditionally would satisfy every arm above.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxIdentifierValidationShould
{
	private const string TestConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

	// Invalid under the identifier allowlist and shaped like the breakout it exists to stop: the '"' closes
	// the double-quote the Qualified*TableName projections apply.
	private const string InjectionPayload = "x\"; DROP TABLE users--";

	private static ServiceProvider BuildProvider(Action<PostgresOutboxStoreOptions> mutate)
	{
		var services = new ServiceCollection();

		_ = services.AddExcaliburOutbox(outbox =>
			outbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));

		// PostConfigure runs before validation, so the wired validator reads the mutated identifier.
		_ = services.PostConfigure(mutate);

		return services.BuildServiceProvider();
	}

	private static PostgresOutboxStoreOptions Resolve(ServiceProvider provider) =>
		provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>().Value;

	/// <summary>
	/// SAFETY: the fence control table name is on the allowlist path like its siblings. RED before the fix —
	/// this was the one identifier in the fencing seam nobody validated.
	/// </summary>
	[Fact]
	public async Task Throws_WhenFenceTableNameIsNotAnAllowlistedIdentifier()
	{
		using var provider = BuildProvider(o => o.FenceTableName = InjectionPayload);

		var ex = await Should.ThrowAsync<OptionsValidationException>(() => Task.Run(() => Resolve(provider)));

		ex.Message.ShouldContain("Fence table name");
	}

	/// <summary>
	/// STRUCTURAL: every table-name identifier on the options type is refused, discovered from the type rather
	/// than listed here — so an identifier added later is covered without this test being edited.
	/// </summary>
	[Fact]
	public void RefuseEveryTableNameIdentifierOnTheOptionsType()
	{
		var identifiers = TableNameProperties();

		identifiers.Length.ShouldBeGreaterThan(
			0,
			"the enumeration found no identifiers to check, so a green here would prove nothing about any of them");

		foreach (var property in identifiers)
		{
			using var provider = BuildProvider(o => property.SetValue(o, InjectionPayload));

			_ = Should.Throw<OptionsValidationException>(
				() => Resolve(provider),
				$"{property.Name} is interpolated into statement text but is not validated against the identifier "
				+ "allowlist. An identifier cannot be parameterized, so the allowlist is the only guard it has.");
		}
	}

	/// <summary>
	/// LIVENESS: the default, wholly valid configuration still resolves — so the arms above are not satisfied
	/// by a validator that refuses every input.
	/// </summary>
	[Fact]
	public void ResolvesCleanly_WhenEveryIdentifierIsValid()
	{
		using var provider = BuildProvider(o => o.FenceTableName = "outbox_fence_2");

		Resolve(provider).FenceTableName.ShouldBe("outbox_fence_2");
	}

	private static PropertyInfo[] TableNameProperties() =>
		[.. typeof(PostgresOutboxStoreOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(static p =>
				p.PropertyType == typeof(string)
				&& p.CanWrite
				&& p.Name.EndsWith("TableName", StringComparison.Ordinal))];
}
