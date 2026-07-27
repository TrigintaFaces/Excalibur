// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Tests.Compliance;

/// <summary>
/// Locks the <c>AutoCreateSchema</c> default across every compliance options type that exposes it.
/// </summary>
/// <remarks>
/// <para>
/// The default is <see langword="false" />: schema provisioning is opt-in, not automatic. An application
/// connection holding schema-DDL privileges in production is a posture most regulated environments refuse,
/// which is why Microsoft ships EF Core's <c>EnsureCreated</c> as an explicit opt-in with migrations as the
/// production path. With the default <see langword="false" />, a store VERIFIES its required tables exist at
/// startup and FAILS FAST if they do not — it never silently skips, so a missing schema surfaces at boot,
/// not at a consumer's first erasure request. Any type that defaults this to <see langword="true" /> would
/// re-introduce the auto-DDL-in-production posture this default exists to prevent.
/// </para>
/// <para>
/// The types are <b>discovered</b> rather than listed. A hand-written list locks only the classes that
/// existed when it was written, so a seventh options type would be silently uncovered — which is the
/// condition this test exists to prevent, reintroduced by the test itself.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance.Erasure")]
public sealed class AutoCreateSchemaDefaultShould : UnitTestBase
{
	/// <summary>
	/// The number of options types carrying <c>AutoCreateSchema</c> at the time this lock was written:
	/// three SQL Server (Erasure, DataInventory, LegalHold) and three Postgres. The floor exists so the
	/// discovery cannot pass by finding nothing — an assembly that fails to load, a renamed property, or a
	/// dropped project reference would otherwise turn this lock green while enforcing nothing.
	/// </summary>
	private const int KnownOptionsTypeCount = 6;

	private const string PropertyName = "AutoCreateSchema";

	private static IReadOnlyList<(Type Type, PropertyInfo Property)> DiscoverOptionsTypes()
	{
		// Anchor on one concrete type per provider assembly so discovery follows the project references
		// rather than a scan of whatever happens to be loaded in the test host.
		var assemblies = new[]
		{
			typeof(Excalibur.Compliance.SqlServer.Erasure.SqlServerErasureStoreOptions).Assembly,
			typeof(Excalibur.Compliance.Postgres.Erasure.PostgresErasureStoreOptions).Assembly,
		};

		return assemblies
			.SelectMany(a => a.GetTypes())
			.Select(t => (Type: t, Property: t.GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance)))
			.Where(x => x.Property is not null && x.Property.PropertyType == typeof(bool))
			.Select(x => (x.Type, Property: x.Property!))
			.OrderBy(x => x.Type.FullName, StringComparer.Ordinal)
			.ToList();
	}

	[Fact]
	public void DiscoverEveryOptionsTypeCarryingTheProperty()
	{
		// LIVENESS: the discovery must actually find the surface. A lock that scans nothing passes
		// regardless of what the defaults are, so the count is asserted before the values.
		var discovered = DiscoverOptionsTypes();

		discovered.Count.ShouldBeGreaterThanOrEqualTo(
			KnownOptionsTypeCount,
			$"discovery found {discovered.Count} options types carrying '{PropertyName}' but at least "
			+ $"{KnownOptionsTypeCount} are known to exist. Either a provider assembly is no longer "
			+ "referenced, or the property was renamed — in both cases this lock is enforcing nothing.");
	}

	[Fact]
	public void DefaultAutoCreateSchemaToFalseOnEveryOptionsType()
	{
		var discovered = DiscoverOptionsTypes();
		var offenders = new List<string>();

		foreach (var (type, property) in discovered)
		{
			// Parameterless construction is the consumer's path: `new XOptions()` then override what they
			// care about. Whatever that yields IS the shipped default.
			var instance = Activator.CreateInstance(type);
			var value = (bool)property.GetValue(instance)!;

			if (value)
			{
				offenders.Add(type.FullName!);
			}
		}

		offenders.ShouldBeEmpty(
			$"these compliance options types default '{PropertyName}' to true: {string.Join(", ", offenders)}. "
			+ "Provisioning is opt-in: a default of true grants the application connection schema-DDL rights in "
			+ "production, the posture regulated consumers refuse. The default is false — the store verifies its "
			+ "schema at startup and fails fast if it is absent. If the default must change back, the consumer "
			+ "documentation has to change with it in the same commit.");
	}
}
