// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain.Specifications;

/// <summary>
/// A specification that is satisfied when both inner specifications are satisfied.
/// </summary>
/// <typeparam name="T">The type to evaluate.</typeparam>
internal sealed class AndSpecification<T> : Specification<T>
{
	private readonly ISpecification<T> _left;
	private readonly ISpecification<T> _right;

	/// <summary>
	/// Initializes a new instance of the <see cref="AndSpecification{T}"/> class.
	/// </summary>
	/// <param name="left">The left specification.</param>
	/// <param name="right">The right specification.</param>
	internal AndSpecification(ISpecification<T> left, ISpecification<T> right)
	{
		_left = left;
		_right = right;
	}

	/// <inheritdoc/>
	public override bool IsSatisfiedBy(T candidate) =>
		_left.IsSatisfiedBy(candidate) && _right.IsSatisfiedBy(candidate);
}
