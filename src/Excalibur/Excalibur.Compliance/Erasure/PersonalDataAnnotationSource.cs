// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Supplies the set of <see cref="PersonalDataCategory"/> values that are actually present on
/// <see cref="PersonalDataAttribute"/>-annotated members in the domain model. Used by the erasure
/// coverage gate (vxp56x) to detect annotated personal data that has no registered/discovered location —
/// so an erasure certificate is never reported "Completed" while annotated data was silently skipped.
/// </summary>
internal interface IPersonalDataAnnotationSource
{
	/// <summary>
	/// Gets the distinct personal-data categories declared via <see cref="PersonalDataAttribute"/>.
	/// </summary>
	/// <returns>The set of annotated categories present in the domain.</returns>
	IReadOnlySet<PersonalDataCategory> GetAnnotatedCategories();

	/// <summary>
	/// Creates the default reflection-based source. Kept as a factory so consumers (e.g. ErasureService)
	/// reference only this interface, not the concrete implementation (class-coupling budget).
	/// </summary>
	static IPersonalDataAnnotationSource CreateDefault() => new ReflectionPersonalDataAnnotationSource();
}

/// <summary>
/// Default <see cref="IPersonalDataAnnotationSource"/> that scans loaded assemblies for
/// <see cref="PersonalDataAttribute"/>-annotated properties, mirroring the reflection scan used by
/// <c>RetentionEnforcementService</c>. This runs on the admin/compliance erasure path (not a consumer
/// hot path), so the reflection cost is acceptable; AOT consumers rely on registration-based coverage.
/// </summary>
internal sealed class ReflectionPersonalDataAnnotationSource : IPersonalDataAnnotationSource
{
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "Annotation discovery uses reflection by design on the admin/compliance erasure path; " +
		"AOT consumers rely on registration-based coverage. Mirrors RetentionEnforcementService.")]
	[UnconditionalSuppressMessage("Trimming", "IL2070:UnrecognizedReflectionPattern",
		Justification = "GetProperties over loaded domain types for [PersonalData] discovery; admin path, not AOT-critical.")]
	public IReadOnlySet<PersonalDataCategory> GetAnnotatedCategories()
	{
		var categories = new HashSet<PersonalDataCategory>();

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic)
			{
				continue;
			}

			try
			{
				foreach (var type in GetLoadableTypes(assembly))
				{
					foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
					{
						var attr = TryGetPersonalDataAttribute(property);
						if (attr is not null)
						{
							_ = categories.Add(attr.Category);
						}
					}
				}
			}
			catch (ReflectionTypeLoadException)
			{
				// Skip assemblies that fail to load types.
			}
		}

		return categories;
	}

#pragma warning disable IL2026 // RequiresUnreferencedCode — reflection is intentional on this admin path.
	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(static t => t is not null)!;
		}
	}
#pragma warning restore IL2026

	private static PersonalDataAttribute? TryGetPersonalDataAttribute(PropertyInfo property)
	{
		try
		{
			return property.GetCustomAttribute<PersonalDataAttribute>();
		}
		catch (TypeLoadException)
		{
			return null;
		}
	}
}
