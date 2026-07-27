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
	public static IEnumerable<object[]> ConsumerFacingMethods()
	{
		var types = new[]
		{
			typeof(Excalibur.EventSourcing.DependencyInjection.MaterializedViewsBuilderExtensions),
			// namespace: Microsoft.Extensions.DependencyInjection
			typeof(MaterializedViewsServiceCollectionExtensions),
		};

		foreach (var type in types)
		{
			foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				yield return new object[] { type.Name, method.Name, method };
			}
		}
	}

	[Theory]
	[MemberData(nameof(ConsumerFacingMethods))]
	public void NotCarryAotHostileAttributes(string typeName, string methodName, MethodInfo method)
	{
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
