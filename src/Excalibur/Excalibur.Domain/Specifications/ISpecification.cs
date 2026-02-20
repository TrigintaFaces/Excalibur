// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain.Specifications;

/// <summary>
/// Defines a business rule that can be evaluated against a candidate.
/// </summary>
/// <typeparam name="T">The type to evaluate.</typeparam>
public interface ISpecification<in T>
{
	/// <summary>
	/// Determines whether the candidate satisfies this specification.
	/// </summary>
	/// <param name="candidate">The candidate to evaluate.</param>
	/// <returns><see langword="true"/> if the candidate satisfies the specification; otherwise, <see langword="false"/>.</returns>
	bool IsSatisfiedBy(T candidate);
}
