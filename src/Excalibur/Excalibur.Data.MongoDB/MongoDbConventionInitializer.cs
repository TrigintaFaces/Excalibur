// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson.Serialization.Conventions;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Registers shared MongoDB BSON serialization conventions used by the Excalibur framework.
/// </summary>
/// <remarks>
/// Both <see cref="MongoDbRepositoryBase{TDocument}"/> and
/// <see cref="Projections.MongoDbProjectionStore{TProjection}"/> require an
/// <see cref="IgnoreExtraElementsConvention"/> so that framework metadata fields
/// (e.g., <c>_projection</c>) are silently skipped during deserialization of consumer types.
/// This class ensures the convention is registered exactly once per process.
/// </remarks>
internal static class MongoDbConventionInitializer
{
	private static volatile bool s_registered;

	/// <summary>
	/// Ensures the Excalibur BSON conventions are registered with the MongoDB driver.
	/// Safe to call from multiple threads; only the first invocation registers.
	/// </summary>
	internal static void EnsureRegistered()
	{
		if (s_registered)
		{
			return;
		}

		var pack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
		ConventionRegistry.Register("ExcaliburIgnoreExtraElements", pack, _ => true);
		s_registered = true;
	}
}
