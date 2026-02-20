// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using FluentValidation;

namespace Excalibur.Application.Requests.Validation;

/// <summary>
/// Validator for <see cref="IActivity" /> properties. Ensures activity details such as name, display name, description, and type are valid.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public sealed class ActivityValidator<TRequest> : RulesFor<TRequest, IActivity>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityValidator{TRequest}" /> class.
	/// </summary>
	public ActivityValidator()
	{
		_ = RuleFor(static x => x.ActivityName).NotEmpty();
		_ = RuleFor(static x => x.ActivityDisplayName).NotEmpty();
		_ = RuleFor(static x => x.ActivityDescription).NotEmpty();
		_ = RuleFor(static x => x.ActivityType)
			.NotEqual(ActivityType.Unknown)
			.WithMessage("{PropertyName} must not be {PropertyValue}. It should probably be Command or Query.");
	}
}
