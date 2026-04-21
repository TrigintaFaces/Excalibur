// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Provides diagnostic operations for the UTF-8 JSON writer pool.
/// Implementations should implement this alongside <see cref="IUtf8JsonWriterPool"/>.
/// </summary>
internal interface IUtf8JsonWriterPoolDiagnostics
{
	/// <summary>Gets the total number of writers rented from the pool.</summary>
	long TotalRented { get; }

	/// <summary>Gets the total number of writers returned to the pool.</summary>
	long TotalReturned { get; }

	/// <summary>Pre-warms the pool by creating the specified number of writers.</summary>
	void PreWarm(int count);
}
