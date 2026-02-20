// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Options;

/// <summary>
/// Unit tests for <see cref="GooglePubSubOptionsValidator"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Options")]
public sealed class GooglePubSubOptionsValidatorShould
{
	private readonly GooglePubSubOptionsValidator _sut;

	public GooglePubSubOptionsValidatorShould()
	{
		_sut = new GooglePubSubOptionsValidator();
	}

	[Fact]
	public void ImplementIValidateOptions()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IValidateOptions<GooglePubSubOptions>>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(GooglePubSubOptionsValidator).IsNotPublic.ShouldBeTrue();
		typeof(GooglePubSubOptionsValidator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccess()
	{
		// Arrange
		var options = new GooglePubSubOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessWithNamedOptions()
	{
		// Arrange
		var options = new GooglePubSubOptions();

		// Act
		var result = _sut.Validate("GcpProject", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
