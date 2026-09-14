// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Guards the generated <see cref="Resources"/> accessor class against a regeneration that
/// silently drops or empties entries. The class is produced from Resources.resx by
/// StronglyTypedResourceBuilder; nothing else verifies that the accessors it emits still
/// resolve, so a bad regeneration would surface only as a null message at runtime.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ResourcesShould
{
	private static readonly PropertyInfo[] Accessors = typeof(Resources)
		.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		.Where(p => p.PropertyType == typeof(string))
		.ToArray();

	[Fact]
	public void ResolveEveryGeneratedAccessorToANonEmptyString()
	{
		Accessors.ShouldNotBeEmpty();

		foreach (var accessor in Accessors)
		{
			var value = (string?)accessor.GetValue(null);
			value.ShouldNotBeNullOrWhiteSpace($"{accessor.Name} resolved to no string");
		}
	}

	[Fact]
	public void ExposeOneAccessorForEveryStringInTheEmbeddedResource()
	{
		// Catches the other half of a bad regeneration: entries present in the .resx that the
		// generated class no longer exposes at all.
		using var reader = new ResourceReader(
			typeof(Resources).Assembly.GetManifestResourceStream("Excalibur.AuditLogging.Resources.resources")!);

		var embedded = reader.Cast<System.Collections.DictionaryEntry>()
			.Select(e => (string)e.Key)
			.ToHashSet(StringComparer.Ordinal);

		Accessors.Select(a => a.Name).ToHashSet(StringComparer.Ordinal).ShouldBe(embedded, ignoreOrder: true);
	}

	[Fact]
	public void ResolveThroughTheResourceManagerUnderTheInvariantCulture()
	{
		Resources.Culture = CultureInfo.InvariantCulture;

		Resources.ResourceManager.ShouldNotBeNull();
		Resources.RbacAuditAnnotationStore_AnnotatePermissionsRequired.ShouldNotBeNullOrWhiteSpace();
		Resources.AuditLoggingServiceCollectionExtensions_NoAuditStoreRegistrationFound
			.ShouldNotBeNullOrWhiteSpace();
	}
}
