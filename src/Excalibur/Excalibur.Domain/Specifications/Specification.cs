// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain.Specifications;

/// <summary>
/// Base class for specifications with And/Or/Not composition support.
/// </summary>
/// <typeparam name="T">The type to evaluate.</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
	/// <inheritdoc/>
	public abstract bool IsSatisfiedBy(T candidate);

	/// <summary>
	/// Combines this specification with another using a logical AND.
	/// </summary>
	/// <param name="other">The other specification to combine with.</param>
	/// <returns>A new specification that is satisfied when both specifications are satisfied.</returns>
	public Specification<T> And(ISpecification<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);
		return new AndSpecification<T>(this, other);
	}

	/// <summary>
	/// Combines this specification with another using a logical OR.
	/// </summary>
	/// <param name="other">The other specification to combine with.</param>
	/// <returns>A new specification that is satisfied when either specification is satisfied.</returns>
	public Specification<T> Or(ISpecification<T> other)
	{
		ArgumentNullException.ThrowIfNull(other);
		return new OrSpecification<T>(this, other);
	}

	/// <summary>
	/// Creates a negation of this specification.
	/// </summary>
	/// <returns>A new specification that is satisfied when this specification is not satisfied.</returns>
	public Specification<T> Not() => new NotSpecification<T>(this);
}
