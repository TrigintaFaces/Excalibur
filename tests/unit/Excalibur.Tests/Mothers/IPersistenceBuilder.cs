// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Mothers;

/// <summary>
///     Interface for building persistence-related test data
/// </summary>
public interface IPersistenceBuilder
{
	/// <summary>
	///     Creates test entity with specified ID
	/// </summary>
	Task<T> CreateAsync<T>(string id, CancellationToken cancellationToken = default) where T : class, new();

	/// <summary>
	///     Creates multiple test entities
	/// </summary>
	Task<IEnumerable<T>> CreateManyAsync<T>(int count, CancellationToken cancellationToken = default) where T : class, new();

	/// <summary>
	///     Clears all test data
	/// </summary>
	Task ClearAsync(CancellationToken cancellationToken = default);
}
