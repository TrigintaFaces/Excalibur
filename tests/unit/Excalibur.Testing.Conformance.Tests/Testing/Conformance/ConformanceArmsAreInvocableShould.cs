// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Testing.Conformance;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Locks every conformance arm to public visibility, so a consumer can invoke it from anywhere.
/// </summary>
/// <remarks>
/// <para>
/// The toolkit documentation tells a consumer the kits carry public virtual conformance methods, and
/// that they should expose those checks to their test framework. An arm declared protected is reachable
/// only from a derived class, so the exposure a consumer can build for it is narrower than the one the
/// page describes: a wrapper must live on the suite type itself and nowhere else. Widening is additive,
/// so the cost of holding this line is nil and the cost of losing it is a surface that contradicts its
/// own documentation.
/// </para>
/// <para>
/// The arm predicate here is the one the kits' own wiring guards use - an instance method, virtual, no
/// parameters, returning Task or void - narrowed by the naming convention that separates an arm from a
/// lifecycle helper. Cleanup, CleanupAsync, ResetDataAsync and DisposeTransportAsync carry no underscore
/// and are correctly protected; every arm carries one.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConformanceArmsAreInvocableShould
{
	private const BindingFlags Declared =
		BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	[Fact]
	public void ExposeEveryArmOnEveryKitAsPublic()
	{
		var offenders = ConformanceKits()
			.SelectMany(static kit => Arms(kit).Select(arm => $"{kit.Name}.{arm.Name}"))
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		offenders.ShouldBeEmpty(
			$"{offenders.Count} conformance arms are not public, so a consumer can invoke them only from a "
			+ "derived type. The shipped toolkit documentation states the arms are public virtual. Widen "
			+ "them: " + string.Join(", ", offenders));
	}

	[Fact]
	public void FindArmsToInspect_SoAnEmptyResultMeansPublicAndNotAnEmptyQuery()
	{
		// Liveness arm: the assertion above passes when every arm is public AND when the predicate matches
		// nothing at all. Only this arm separates those two states.
		var kits = ConformanceKits();
		kits.Count.ShouldBeGreaterThan(20, "the conformance assembly should expose many kits");

		var armCount = kits.Sum(static kit => AllArms(kit).Count);
		armCount.ShouldBeGreaterThan(500, "the kits should expose hundreds of arms for the guard to see");
	}

	private static List<Type> ConformanceKits() =>
		[.. typeof(ConformanceTestKit).Assembly
			.GetTypes()
			.Where(static t => t.IsClass
				&& t.IsAbstract
				&& t.Name.EndsWith("ConformanceTestKit", StringComparison.Ordinal))
			.OrderBy(static t => t.Name, StringComparer.Ordinal)];

	/// <summary>Every arm on the kit, whatever its visibility.</summary>
	private static List<MethodInfo> AllArms(Type kit) =>
		[.. kit.GetMethods(Declared)
			.Where(static m => (m.ReturnType == typeof(Task) || m.ReturnType == typeof(void))
				&& m.GetParameters().Length == 0
				&& m.IsVirtual
				&& !m.IsSpecialName
				&& m.Name.Contains('_', StringComparison.Ordinal))];

	/// <summary>The arms that a consumer cannot invoke from outside a derived type.</summary>
	private static List<MethodInfo> Arms(Type kit) =>
		[.. AllArms(kit).Where(static m => !m.IsPublic)];
}
