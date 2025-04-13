using Excalibur.Application.Behaviors;
using Excalibur.Tests.Stubs.Application;

using FakeItEasy;

using FluentValidation;
using FluentValidation.Results;

using MediatR;

using Shouldly;

namespace Excalibur.Tests.Unit.Application.Behaviors;

public class ValidationBehaviorShould
{
	[Fact]
	public async Task CallNextWhenNoValidatorsExist()
	{
		// Arrange
		var validators = Array.Empty<IValidator<TestRequest>>();
		var behavior = new ValidationBehaviour<TestRequest, string>(validators);
		var request = new TestRequest("Test");
		var next = A.Fake<RequestHandlerDelegate<string>>();
		const string expected = "Success";

		_ = A.CallTo(() => next()).Returns(expected);

		// Act
		var result = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expected);
		_ = A.CallTo(() => next()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CallNextWhenAllValidationsPass()
	{
		// Arrange
		var validator = A.Fake<IValidator<TestRequest>>();
		var validators = new[] { validator };
		var behavior = new ValidationBehaviour<TestRequest, string>(validators);
		var request = new TestRequest("Test");
		var next = A.Fake<RequestHandlerDelegate<string>>();
		const string expected = "Success";

		_ = A.CallTo(() => validator.ValidateAsync(A<ValidationContext<TestRequest>>._, A<CancellationToken>._))
			.Returns(new ValidationResult());
		_ = A.CallTo(() => next()).Returns(expected);

		// Act
		var result = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expected);
		_ = A.CallTo(() => validator.ValidateAsync(A<ValidationContext<TestRequest>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => next()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowValidationExceptionWhenValidationsFail()
	{
		// Arrange
		var validator = A.Fake<IValidator<TestRequest>>();
		var validators = new[] { validator };
		var behavior = new ValidationBehaviour<TestRequest, string>(validators);
		var request = new TestRequest("Test");
		var next = A.Fake<RequestHandlerDelegate<string>>();

		var validationFailure = new ValidationFailure("Property", "Error message");
		var validationResult = new ValidationResult([validationFailure]);

		_ = A.CallTo(() => validator.ValidateAsync(A<ValidationContext<TestRequest>>._, A<CancellationToken>._))
			.Returns(validationResult);

		// Act & Assert
		var exception = await Should.ThrowAsync<ValidationException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.Errors.ShouldContain(validationFailure);
		A.CallTo(() => next()).MustNotHaveHappened();
	}

	[Fact]
	public async Task AggregateValidationErrorsFromMultipleValidators()
	{
		// Arrange
		var validator1 = A.Fake<IValidator<TestRequest>>();
		var validator2 = A.Fake<IValidator<TestRequest>>();
		var validators = new[] { validator1, validator2 };
		var behavior = new ValidationBehaviour<TestRequest, string>(validators);
		var request = new TestRequest("Test");
		var next = A.Fake<RequestHandlerDelegate<string>>();

		var failure1 = new ValidationFailure("Property1", "Error message 1");
		var failure2 = new ValidationFailure("Property2", "Error message 2");

		_ = A.CallTo(() => validator1.ValidateAsync(A<ValidationContext<TestRequest>>._, A<CancellationToken>._))
			.Returns(new ValidationResult([failure1]));
		_ = A.CallTo(() => validator2.ValidateAsync(A<ValidationContext<TestRequest>>._, A<CancellationToken>._))
			.Returns(new ValidationResult([failure2]));

		// Act & Assert
		var exception = await Should.ThrowAsync<ValidationException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.Errors.ShouldContain(failure1);
		exception.Errors.ShouldContain(failure2);
		A.CallTo(() => next()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenRequestIsNull()
	{
		// Arrange
		var validators = Array.Empty<IValidator<TestRequest>>();
		var behavior = new ValidationBehaviour<TestRequest, string>(validators);
		TestRequest request = null!;
		var next = A.Fake<RequestHandlerDelegate<string>>();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("request");
		A.CallTo(() => next()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenNextIsNull()
	{
		// Arrange
		var validators = Array.Empty<IValidator<TestRequest>>();
		var behavior = new ValidationBehaviour<TestRequest, string>(validators);
		var request = new TestRequest("Test");
		RequestHandlerDelegate<string> next = null!;

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("next");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenValidatorsIsNull()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new ValidationBehaviour<TestRequest, string>(null!));

		exception.ParamName.ShouldBe("validators");
	}
}
