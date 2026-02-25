// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using FakeItEasy;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

/// <summary>
/// Unit tests for <see cref="XssValidator"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Validation")]
public sealed class XssValidatorShould
{
	private readonly XssValidator _sut;
	private readonly IDispatchMessage _message;
	private readonly IMessageContext _context;

	public XssValidatorShould()
	{
		_sut = new XssValidator();
		_message = A.Fake<IDispatchMessage>();
		_context = A.Fake<IMessageContext>();
	}

	[Fact]
	public void ImplementIInputValidator()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IInputValidator>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(XssValidator).IsNotPublic.ShouldBeTrue();
		typeof(XssValidator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccessResult()
	{
		// Act
		var result = await _sut.ValidateAsync(_message, _context);

		// Assert
		result.ShouldNotBeNull();
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnEmptyErrorsOnSuccess()
	{
		// Act
		var result = await _sut.ValidateAsync(_message, _context);

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task CompleteWithoutExceptions()
	{
		// Act & Assert
		await Should.NotThrowAsync(async () => await _sut.ValidateAsync(_message, _context));
	}
}
