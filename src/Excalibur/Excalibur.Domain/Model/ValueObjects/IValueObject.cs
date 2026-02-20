// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model.ValueObjects;

/// <summary>
/// Represents a value object in domain-driven design.
/// </summary>
public interface IValueObject
{
	/// <summary>
	/// Gets the components that contribute to the equality comparison of this value object.
	/// </summary>
	/// <returns> An enumerable collection of equality components. </returns>
	IEnumerable<object?> GetEqualityComponents();
}
