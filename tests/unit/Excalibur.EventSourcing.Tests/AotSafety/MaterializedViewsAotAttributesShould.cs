// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.EventSourcing.Tests.AotSafety;

/// <summary>
/// Guards that the consumer-facing materialized-view registration surface is AOT-safe: no public method a
/// consumer calls may carry <c>[RequiresUnreferencedCode]</c> or <c>[RequiresDynamicCode]</c>, since either
/// attribute propagates an AOT/trim warning into every consuming application. The registration methods are
/// genuinely reflection-free (options are bound with delegate <c>Configure</c> actions, not configuration
/// binding), so the requirement must be absent — not merely suppressed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MaterializedViewsAotAttributesShould
{
	private static readonly Type[] RegistrationSurface =
	[
		typeof(Excalibur.EventSourcing.DependencyInjection.MaterializedViewsBuilderExtensions),
		// namespace: Microsoft.Extensions.DependencyInjection
		typeof(MaterializedViewsServiceCollectionExtensions),
	];

	// One row per declaring type, never per method: a theory argument lands in the test's display name, and
	// VSTest derives its test ID by hashing that name. Anything compiler-assigned there (a metadata token,
	// which an earlier revision carried to keep a MethodInfo out of the row) shifts on recompile, so the
	// test's identity changes between discovery and execution. A Type renders stably, and keying rows by
	// type also removes the ambiguity between overloads. The methods are enumerated in the body instead.
	public static TheoryData<Type> ConsumerFacingTypes()
	{
		var data = new TheoryData<Type>();

		foreach (var type in RegistrationSurface)
		{
			data.Add(type);
		}

		return data;
	}

	[Theory]
	[MemberData(nameof(ConsumerFacingTypes))]
	public void NotCarryAotHostileAttributes(Type declaringType)
	{
		var typeName = declaringType.Name;
		var methods = RegistrationMethods(declaringType);

		// Liveness: the safety assertions below are vacuous for a type that enumerates no methods, so a
		// surface that was removed or renamed must redden here rather than pass by examining nothing.
		methods.ShouldNotBeEmpty($"{typeName} exposes no public static methods — the AOT guard has nothing to check.");

		foreach (var method in methods)
		{
			method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull(
				$"{typeName}.{method.Name} is consumer-facing and must not carry [RequiresUnreferencedCode]; "
				+ "remove the reflection requirement rather than suppress or propagate it.");

			method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull(
				$"{typeName}.{method.Name} is consumer-facing and must not carry [RequiresDynamicCode]; "
				+ "remove the reflection requirement rather than suppress or propagate it.");
		}
	}

	[Fact]
	public void EnumerateTheRegistrationSurface()
	{
		// Liveness: the theory above is only meaningful if the surface it iterates is non-empty. A change
		// that removed the registration types entirely must not silently reduce this guard to zero cases.
		RegistrationSurface.SelectMany(RegistrationMethods).ShouldNotBeEmpty();
	}

	private static MethodInfo[] RegistrationMethods(Type type) =>
		type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
}
