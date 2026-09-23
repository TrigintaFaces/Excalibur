// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

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
	/// Gets the distinct personal-data categories declared via <see cref="PersonalDataAttribute"/>,
	/// together with whether the scan that produced them was actually <em>established</em>.
	/// </summary>
	/// <returns>The annotated categories, and whether they are a complete answer.</returns>
	/// <remarks>
	/// The second half is the whole contract. An empty category set has two meanings — <em>the domain
	/// annotates no personal data</em> and <em>the scan could not see it</em> — and a coverage gate that
	/// cannot tell them apart reports the second as the first, which is a certificate attesting to an
	/// absence of evidence. Returning a bare set makes the distinction unsayable, so it must not be a
	/// bare set.
	/// </remarks>
	PersonalDataAnnotationScan GetAnnotatedCategories();

	/// <summary>
	/// Creates the default reflection-based source. Kept as a factory so consumers (e.g. ErasureService)
	/// reference only this interface, not the concrete implementation (class-coupling budget).
	/// </summary>
	static IPersonalDataAnnotationSource CreateDefault() => new ReflectionPersonalDataAnnotationSource();
}

/// <summary>
/// The annotated categories a scan found, and whether the scan was complete enough for their absence to
/// mean anything.
/// </summary>
/// <param name="Categories">The distinct annotated categories the scan observed.</param>
/// <param name="ScanEstablished">
/// <see langword="true"/> only when the scan examined everything it set out to examine. When
/// <see langword="false"/>, <paramref name="Categories"/> is a lower bound and nothing may be concluded
/// from a category's absence from it.
/// </param>
/// <remarks>
/// The deliberate asymmetry: a <see langword="true"/> here is a claim about the whole domain, while a
/// <see langword="false"/> claims only that something was missed. So every path that cannot prove
/// completeness reports <see langword="false"/>, and the gate treats that as unestablished rather than
/// as clean.
/// </remarks>
internal readonly record struct PersonalDataAnnotationScan(
	IReadOnlySet<PersonalDataCategory> Categories,
	bool ScanEstablished);

/// <summary>
/// Default <see cref="IPersonalDataAnnotationSource"/> that scans loaded assemblies for
/// <see cref="PersonalDataAttribute"/>-annotated properties. Its width is deliberately ambient: it is a
/// fail-closed coverage gate, so a missed type makes erasure refuse to complete. (Retention, which deletes,
/// takes the opposite shape and acts only on a declared scope.) This runs on the admin/compliance erasure path (not a consumer
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
			+ "The scan therefore cannot be trusted to be complete under trimming, and it does not claim to "
			+ "be: it reports ScanEstablished=false whenever reflection is unsupported or any part of the "
			+ "walk was skipped, and the coverage gate refuses to report an erasure Completed on an "
			+ "unestablished scan.")]
	public PersonalDataAnnotationScan GetAnnotatedCategories()
	{
		var categories = new HashSet<PersonalDataCategory>();

		// Asked before any work is done, because the answer invalidates the whole walk rather than part of
		// it. Where dynamic code is unsupported the trimmer has already decided which members survive, and
		// a scan over what remains cannot distinguish "not annotated" from "not preserved" -- so the result
		// is a lower bound no matter how cleanly it runs.
		var established = RuntimeFeature.IsDynamicCodeSupported;

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic)
			{
				continue;
			}

			try
			{
				foreach (var type in GetLoadableTypes(assembly, ref established))
				{
					foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
					{
						var attr = TryGetPersonalDataAttribute(property, ref established);
						if (attr is not null)
						{
							_ = categories.Add(attr.Category);
						}
					}
				}
			}
			catch (ReflectionTypeLoadException)
			{
				// An assembly we could not read at all. Previously this skipped silently, which is how a
				// partial scan became indistinguishable from a complete one: the categories that assembly
				// declared are simply absent from the result, and absence is what the gate reads as
				// "covered".
				established = false;
			}
		}

		return new PersonalDataAnnotationScan(categories, established);
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "Assembly.GetTypes() over host-loaded assemblies is the erasure-inventory scan itself. "
			+ "A partial type list clears the established flag rather than being returned as if whole, so a "
			+ "trimmed host yields an unestablished scan and the coverage gate refuses to complete on it.")]
	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly, ref bool established)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			// A PARTIAL type list, and the partiality is the finding. Returning the readable subset is
			// right; returning it as though it were the whole assembly is what made a narrowed scan
			// indistinguishable from a clean one.
			established = false;
			return ex.Types.Where(static t => t is not null)!;
		}
	}

	private static PersonalDataAttribute? TryGetPersonalDataAttribute(PropertyInfo property, ref bool established)
	{
		try
		{
			return property.GetCustomAttribute<PersonalDataAttribute>();
		}
		catch (TypeLoadException)
		{
			// The attribute may or may not have been there; we could not look. That is not the same as
			// having looked and found nothing, and only one of those two is safe to certify on.
			established = false;
			return null;
		}
	}
}
