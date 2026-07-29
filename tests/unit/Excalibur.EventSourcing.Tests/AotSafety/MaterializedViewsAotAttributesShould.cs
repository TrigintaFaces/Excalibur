// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	// Rows carry the declaring type plus the method's metadata token rather than a MethodInfo: a MethodInfo
	// type argument is not reliably serializable (xUnit1045), which stops the runner enumerating individual
	// data rows. The token resolves back to exactly the same method — one row per method, as before.
	public static TheoryData<Type, string, int> ConsumerFacingMethods()
	{
		var types = new[]
		{
			typeof(Excalibur.EventSourcing.DependencyInjection.MaterializedViewsBuilderExtensions),
			// namespace: Microsoft.Extensions.DependencyInjection
			typeof(MaterializedViewsServiceCollectionExtensions),
		};

		var data = new TheoryData<Type, string, int>();

		foreach (var type in types)
		{
			foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				data.Add(type, method.Name, method.MetadataToken);
			}
		}

		return data;
	}

	[Theory]
	[MemberData(nameof(ConsumerFacingMethods))]
	public void NotCarryAotHostileAttributes(Type declaringType, string methodName, int metadataToken)
	{
		var typeName = declaringType.Name;
		var method = (MethodInfo)declaringType.Module.ResolveMethod(metadataToken)!;

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull(
			$"{typeName}.{methodName} is consumer-facing and must not carry [RequiresUnreferencedCode]; "
			+ "remove the reflection requirement rather than suppress or propagate it.");

		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull(
			$"{typeName}.{methodName} is consumer-facing and must not carry [RequiresDynamicCode]; "
			+ "remove the reflection requirement rather than suppress or propagate it.");
	}

	[Fact]
	public void EnumerateTheRegistrationSurface()
	{
		// Liveness: the theory above is only meaningful if it actually enumerates the registration methods.
		// A change that removed or renamed the surface must not silently reduce this guard to zero cases.
		ConsumerFacingMethods().ShouldNotBeEmpty();
	}
}
