// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain.Specifications;

/// <summary>
/// A specification that is satisfied when the inner specification is not satisfied.
/// </summary>
/// <typeparam name="T">The type to evaluate.</typeparam>
internal sealed class NotSpecification<T> : Specification<T>
{
	private readonly ISpecification<T> _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="NotSpecification{T}"/> class.
	/// </summary>
	/// <param name="inner">The specification to negate.</param>
	internal NotSpecification(ISpecification<T> inner)
	{
		_inner = inner;
	}

	/// <inheritdoc/>
	public override bool IsSatisfiedBy(T candidate) =>
		!_inner.IsSatisfiedBy(candidate);
}
