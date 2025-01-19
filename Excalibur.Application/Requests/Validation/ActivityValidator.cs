using FluentValidation;

namespace Excalibur.Application.Requests.Validation;

/// <summary>
///     Validator for <see cref="IActivity" /> properties. Ensures activity details such as name, display name, description, and type are valid.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public class ActivityValidator<TRequest> : RulesFor<TRequest, IActivity>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ActivityValidator{TRequest}" /> class.
	/// </summary>
	public ActivityValidator()
	{
		_ = RuleFor(x => x.ActivityName).NotEmpty();
		_ = RuleFor(x => x.ActivityDisplayName).NotEmpty();
		_ = RuleFor(x => x.ActivityDescription).NotEmpty();
		_ = RuleFor(x => x.ActivityType)
			.NotEqual(ActivityType.Unknown)
			.WithMessage("{PropertyName} must not be {PropertyValue}. It should probably be Command or Query.");
	}
}
