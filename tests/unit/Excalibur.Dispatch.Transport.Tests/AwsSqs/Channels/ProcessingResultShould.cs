// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ProcessingResultShould
{
	[Fact]
	public void CreateSuccessResult()
	{
		// Arrange & Act
		var result = ProcessingResult.Ok();

		// Assert
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResult()
	{
		// Arrange & Act
		var result = ProcessingResult.Failed("timeout");

		// Assert
		result.Success.ShouldBeFalse();
		result.Error.ShouldBe("timeout");
	}

	[Fact]
	public void SupportEqualityForEqualResults()
	{
		// Arrange
		var a = ProcessingResult.Ok();
		var b = ProcessingResult.Ok();

		// Act & Assert
		a.Equals(b).ShouldBeTrue();
		(a == b).ShouldBeTrue();
		(a != b).ShouldBeFalse();
	}

	[Fact]
	public void SupportInequalityForDifferentResults()
	{
		// Arrange
		var ok = ProcessingResult.Ok();
		var failed = ProcessingResult.Failed("err");

		// Act & Assert
		ok.Equals(failed).ShouldBeFalse();
		(ok == failed).ShouldBeFalse();
		(ok != failed).ShouldBeTrue();
	}

	[Fact]
	public void SupportObjectEquals()
	{
		// Arrange
		var a = ProcessingResult.Ok();
		object b = ProcessingResult.Ok();

		// Act & Assert
		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForObjectEqualsWithDifferentType()
	{
		// Arrange
		var a = ProcessingResult.Ok();

		// Act & Assert
		a.Equals("not a result").ShouldBeFalse();
	}

	[Fact]
	public void ReturnConsistentHashCode()
	{
		// Arrange
		var a = ProcessingResult.Ok();
		var b = ProcessingResult.Ok();

		// Act & Assert
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void ReturnDifferentHashCodeForDifferentErrors()
	{
		// Arrange
		var a = ProcessingResult.Failed("err1");
		var b = ProcessingResult.Failed("err2");

		// Act & Assert
		a.GetHashCode().ShouldNotBe(b.GetHashCode());
	}
}
