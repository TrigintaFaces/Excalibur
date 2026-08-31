// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.TypeResolution;

/// <summary>
/// Serialises every test class that registers into the process-wide type-resolver registry. The registry
/// is static, so two classes mutating it at once would see each other's registrations.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TypeResolverRegistryCollection
{
	/// <summary>
	/// The collection name shared by the classes that mutate the registry.
	/// </summary>
	public const string Name = "TypeResolverRegistry";
}
