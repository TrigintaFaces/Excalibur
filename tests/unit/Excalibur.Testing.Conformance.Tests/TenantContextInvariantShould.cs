// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;

using Excalibur.Testing.Conformance;

namespace Excalibur.Tests.Tenancy;

/// <summary>
/// Enumerates every <see cref="ITenantContext"/> implementation reachable in the solution and asserts the
/// interface's stated invariant: <c>HasTenant</c> is <see langword="true"/> exactly when <c>TenantId</c> is a
/// non-null, NON-EMPTY identifier.
/// </summary>
/// <remarks>
/// <para>
/// The invariant is a behavioural-subtyping constraint, so it cannot be checked on one representative
/// implementation: an implementation that reports a tenant present while exposing an empty id weakens the
/// contract for every consumer that branches on <c>HasTenant</c>. The three conformance kits shipped in
/// <c>Excalibur.Testing.Conformance</c> are the case that motivated this lock -- a provider verified against a
/// kit whose own tenant context disagrees with the contract is verified against the wrong contract.
/// </para>
/// <para>
/// NON-VACUITY. The empty-string arm is the one that discriminates: <c>TenantId is not null</c> and
/// <c>!string.IsNullOrEmpty(TenantId)</c> agree on null and on a real identifier, and differ ONLY on
/// <see cref="string.Empty"/>. A run that never drove an implementation to the empty string would pass against
/// both spellings and prove nothing, so <see cref="EveryMutableImplementation_ReportsNoTenant_ForTheEmptyString"/>
/// asserts that the mutable implementations were actually found and exercised rather than silently skipped.
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Component", "Tenancy")]
public sealed class TenantContextInvariantShould
{
	/// <summary>
	/// Concrete <see cref="ITenantContext"/> implementations discovered across the loaded Excalibur assemblies,
	/// including private nested types (the conformance kits declare theirs as nested private classes).
	/// </summary>
	private static IReadOnlyList<Type> Implementations()
	{
		// Anchor the assemblies that declare implementations by taking a REFERENCE to one of their types.
		// `_ = typeof(T);` is not sufficient: it is a no-op the compiler may elide, so the assembly is never
		// loaded, it never appears in the AppDomain enumeration, and the census reports a confident zero.
		var anchors = new[]
		{
			typeof(EventStoreConformanceTestKit).Assembly,   // Excalibur.Testing.Conformance -- the shipped kits
			typeof(ITenantContext).Assembly,                 // the contract, and the compliant implementations beside it
		};

		return [.. anchors
			.Concat(AppDomain.CurrentDomain.GetAssemblies())
			.Where(static a => !a.IsDynamic && (a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) ?? false))
			// SHIPPED code only. A test assembly's own doubles are not the subject: this lock is about what a
			// consumer receives, and a double's tenancy semantics belong to the suite that owns it.
			.Where(static a => !(a.GetName().Name?.Contains(".Tests", StringComparison.Ordinal) ?? false))
			.Distinct()
			.SelectMany(static a =>
			{
				try
				{
					return a.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					return ex.Types.OfType<Type>().ToArray();
				}
			})
			.Where(static t => t is { IsClass: true, IsAbstract: false } && typeof(ITenantContext).IsAssignableFrom(t))
			.Distinct()
			.OrderBy(static t => t.FullName, StringComparer.Ordinal)];
	}

	/// <summary>Creates an instance, preferring a singleton accessor, then a (possibly non-public) parameterless constructor.</summary>
	private static ITenantContext? TryCreate(Type type)
	{
		var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
		if (instanceProperty?.GetValue(null) is ITenantContext singleton)
		{
			return singleton;
		}

		try
		{
			return Activator.CreateInstance(type, nonPublic: true) as ITenantContext;
		}
		catch (MissingMethodException)
		{
			return null;
		}
		catch (TargetInvocationException)
		{
			return null;
		}
	}

	/// <summary>Returns the action that drives <c>TenantId</c> to an arbitrary value, when the type exposes one.</summary>
	private static Action<ITenantContext, string>? TryFindTenantSetter(Type type)
	{
		var switchTo = type.GetMethod("SwitchTo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(string)]);
		if (switchTo is not null)
		{
			return (context, tenantId) => switchTo.Invoke(context, [tenantId]);
		}

		var tenantId = type.GetProperty(nameof(ITenantContext.TenantId), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		var setter = tenantId?.GetSetMethod(nonPublic: true);

		return setter is null ? null : (context, value) => setter.Invoke(context, [value]);
	}

	/// <summary>
	/// Safety arm. Every implementation, in whatever state it can be constructed in, must keep the two members
	/// consistent.
	/// </summary>
	[Fact]
	public void EveryImplementation_KeepsHasTenantConsistentWithTenantId_AsConstructed()
	{
		var violations = new List<string>();
		var exercised = 0;

		foreach (var type in Implementations())
		{
			var context = TryCreate(type);
			if (context is null)
			{
				continue;
			}

			exercised++;

			var expected = !string.IsNullOrEmpty(context.TenantId);
			if (context.HasTenant != expected)
			{
				violations.Add(string.Create(
					CultureInfo.InvariantCulture,
					$"{type.FullName}: TenantId={Describe(context.TenantId)} but HasTenant={context.HasTenant} (expected {expected})"));
			}
		}

		exercised.ShouldBeGreaterThan(0, "the census found no constructible ITenantContext implementation -- the scan, not the contract, is broken");
		violations.ShouldBeEmpty();
	}

	/// <summary>
	/// The discriminating arm. An implementation whose tenant can be driven to <see cref="string.Empty"/> must
	/// report NO tenant: this is the only input on which <c>TenantId is not null</c> and
	/// <c>!string.IsNullOrEmpty(TenantId)</c> disagree, so it is the arm that fails on the weaker spelling.
	/// </summary>
	[Fact]
	public void EveryMutableImplementation_ReportsNoTenant_ForTheEmptyString()
	{
		var violations = new List<string>();
		var exercised = new List<string>();

		foreach (var type in Implementations())
		{
			var setTenant = TryFindTenantSetter(type);
			if (setTenant is null)
			{
				continue;
			}

			var context = TryCreate(type);
			if (context is null)
			{
				continue;
			}

			setTenant(context, string.Empty);
			exercised.Add(type.FullName ?? type.Name);

			if (context.HasTenant)
			{
				violations.Add($"{type.FullName}: HasTenant is true while TenantId is the empty string");
			}
		}

		// Liveness: the three shipped conformance kits each declare a switchable context. If the census stops
		// finding them (renamed, made non-nested, no longer mutable) this arm would pass by exercising nothing.
		exercised.Count.ShouldBeGreaterThanOrEqualTo(
			3,
			$"expected at least the three shipped conformance-kit tenant contexts to be exercised, found: {string.Join(", ", exercised)}");

		violations.ShouldBeEmpty();
	}

	private static string Describe(string? value) => value is null ? "<null>" : $"\"{value}\"";
}
