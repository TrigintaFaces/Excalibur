// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Compliance.Retention;

/// <summary>
/// The retention policies a host has explicitly placed in scope, through <c>AddRetentionPolicies&lt;T&gt;()</c>
/// or <c>AddRetentionPoliciesFromAssembly(Assembly)</c>. Retention enforcement acts on the union of these
/// declarations and on nothing else.
/// </summary>
/// <remarks>
/// The population is computed when it is declared, from the named type or assembly only. It is never
/// discovered from whatever assemblies the process happens to have loaded: enforcement deletes, and a
/// deletion scope the host did not state is one it cannot audit.
/// </remarks>
/// <param name="Source">What was declared, for diagnostics.</param>
/// <param name="Policies">The retention policies the declaration contributes. Never empty.</param>
internal sealed record RetentionPolicyDeclaration(string Source, IReadOnlyList<RetentionPolicy> Policies)
{
	internal static RetentionPolicyDeclaration ForType(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
	{
		var policies = new List<RetentionPolicy>();
		AddPolicies(type, policies);

		if (policies.Count == 0)
		{
			throw new ArgumentException(
				$"Type '{type.FullName}' declares no [PersonalData] property with a positive RetentionDays, so it cannot be placed in retention scope.",
				nameof(type));
		}

		return new RetentionPolicyDeclaration(type.FullName ?? type.Name, policies);
	}

	[RequiresUnreferencedCode("Enumerates the types of the declared assembly and reads their public properties, which trimming may remove.")]
	internal static RetentionPolicyDeclaration ForAssembly(Assembly assembly)
	{
		var policies = new List<RetentionPolicy>();

		// A partial type list is not silently accepted: GetTypes throws ReflectionTypeLoadException and the
		// declaration fails, because a retention scope with an unknown hole in it is not a declared scope.
		foreach (var type in assembly.GetTypes())
		{
			AddPolicies(type, policies);
		}

		if (policies.Count == 0)
		{
			throw new ArgumentException(
				$"Assembly '{assembly.GetName().Name}' declares no [PersonalData] property with a positive RetentionDays, so it cannot be placed in retention scope.",
				nameof(assembly));
		}

		return new RetentionPolicyDeclaration(assembly.GetName().Name ?? assembly.FullName ?? "assembly", policies);
	}

	private static void AddPolicies(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
		List<RetentionPolicy> policies)
	{
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var attr = property.GetCustomAttribute<PersonalDataAttribute>();
			if (attr is null || attr.RetentionDays <= 0)
			{
				continue;
			}

			policies.Add(new RetentionPolicy
			{
				TypeName = type.FullName ?? type.Name,
				PropertyName = property.Name,
				Category = attr.Category,
				RetentionDays = attr.RetentionDays,
			});
		}
	}
}
