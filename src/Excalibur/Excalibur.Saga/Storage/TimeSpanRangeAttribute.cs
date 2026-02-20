// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Excalibur.Saga.Storage;

/// <summary>
/// Validates that a <see cref="TimeSpan"/> value falls within the configured inclusive range without relying on trim-unsafe type converters.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TimeSpanRangeAttribute"/> class.
/// </remarks>
/// <param name="minimum">Minimum supported <see cref="TimeSpan"/> expressed as <c>hh:mm:ss</c>.</param>
/// <param name="maximum">Maximum supported <see cref="TimeSpan"/> expressed as <c>hh:mm:ss</c>.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
internal sealed class TimeSpanRangeAttribute(string minimum, string maximum) : ValidationAttribute
{
	/// <summary>
	/// Gets the inclusive minimum value allowed.
	/// </summary>
	/// <value>The inclusive minimum value allowed.</value>
	public TimeSpan Minimum { get; } = TimeSpan.Parse(minimum, CultureInfo.InvariantCulture);

	/// <summary>
	/// Gets the inclusive maximum value allowed.
	/// </summary>
	/// <value>The inclusive maximum value allowed.</value>
	public TimeSpan Maximum { get; } = TimeSpan.Parse(maximum, CultureInfo.InvariantCulture);

	/// <inheritdoc/>
	public override bool IsValid(object? value)
	{
		if (value is null)
		{
			return true;
		}

		if (value is TimeSpan timeSpan)
		{
			return timeSpan >= Minimum && timeSpan <= Maximum;
		}

		return false;
	}

	/// <inheritdoc/>
	public override string FormatErrorMessage(string name) =>
		$"The field {name} must be between {Minimum:c} and {Maximum:c}.";
}

