// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Supplies the set of <see cref="PersonalDataCategory"/> values that are actually present on
/// <see cref="PersonalDataAttribute"/>-annotated members in the domain model. Used by the erasure
/// coverage gate to detect annotated personal data that has no registered/discovered location —
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
	// The scan has no statically known type set by construction: it enumerates whatever assemblies the
	// host loaded, so the trimmer cannot prove the properties it reads are preserved. That is the
	// diagnostic below, named exactly. Ahead-of-time consumers use the registration-based source
	// instead, and the coverage gate fails closed rather than reporting an erasure complete.
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:UnrecognizedReflectionPattern",
		Justification = "Types come from a whole-domain assembly scan, so no annotation can flow to them. "
			+ "This is an administrative erasure-inventory path; ahead-of-time hosts use the registration-based "
			+ "source, and a category the scan misses fails the coverage gate rather than completing silently.")]
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

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "Assembly.GetTypes() over host-loaded assemblies is the erasure-inventory scan itself; "
			+ "a trimmed host uses the registration-based source, and a missed category fails the coverage gate.")]
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
