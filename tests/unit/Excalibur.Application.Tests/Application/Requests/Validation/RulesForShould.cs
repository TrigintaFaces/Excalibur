// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.Tests.Application.Requests.Validation;

/// <summary>
/// Unit tests for <see cref="RulesFor{TRequest, TPart}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class RulesForShould
{
	[Fact]
	public void Validate_ValidRequest_ReturnsIsValid()
	{
		// Arrange
		IValidator<TestRequest> validator = new TestRequestPartValidator();
		var request = new TestRequest { Name = "Valid", Age = 25 };

		// Act
		var result = validator.Validate(request);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Validate_InvalidRequest_ReturnsErrors()
	{
		// Arrange
		IValidator<TestRequest> validator = new TestRequestPartValidator();
		var request = new TestRequest { Name = "", Age = 25 };

		// Act
		var result = validator.Validate(request);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Name");
	}

	[Fact]
	public async Task ValidateAsync_ValidRequest_ReturnsIsValid()
	{
		// Arrange
		IValidator<TestRequest> validator = new TestRequestPartValidator();
		var request = new TestRequest { Name = "Valid", Age = 25 };

		// Act
		var result = await validator.ValidateAsync(request, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateAsync_InvalidRequest_ReturnsErrors()
	{
		// Arrange
		IValidator<TestRequest> validator = new TestRequestPartValidator();
		var request = new TestRequest { Name = "", Age = 25 };

		// Act
		var result = await validator.ValidateAsync(request, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Validate_NullRequest_ThrowsArgumentNullException()
	{
		// Arrange
		IValidator<TestRequest> validator = new TestRequestPartValidator();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			validator.Validate((TestRequest)null!));
	}

	#region Test Types

	private sealed class TestRequest : ITestPart
	{
		public string Name { get; set; } = string.Empty;
		public int Age { get; set; }
	}

	private interface ITestPart
	{
		string Name { get; }
	}

	private sealed class TestRequestPartValidator : RulesFor<TestRequest, TestRequest>
	{
		public TestRequestPartValidator()
		{
			RuleFor(x => x.Name).NotEmpty();
		}
	}

	#endregion
}
