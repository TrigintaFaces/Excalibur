// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Unit tests for <see cref="GrpcTransportOptionsValidator"/>.
/// Sprint 697 T.33: gRPC transport test coverage.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class GrpcTransportOptionsValidatorShould
{
	private readonly GrpcTransportOptionsValidator _sut = new();

	#region Success Cases

	[Fact]
	public void Succeed_WithValidHttpsAddress()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_WithValidHttpAddress()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "http://localhost:5000",
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_WithNullMessageSizes()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			MaxSendMessageSize = null,
			MaxReceiveMessageSize = null,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_WithValidMessageSizes()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			MaxSendMessageSize = 4_194_304,    // 4MB
			MaxReceiveMessageSize = 4_194_304,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region ServerAddress Failures

	[Fact]
	public void Fail_WhenServerAddressIsEmpty()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "",
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ServerAddress");
	}

	[Fact]
	public void Fail_WhenServerAddressIsWhitespace()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "   ",
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ServerAddress");
	}

	[Fact]
	public void Fail_WhenServerAddressIsNotAbsoluteUri()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "not-a-uri",
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("not a valid absolute URI");
	}

	[Fact]
	public void Fail_WhenServerAddressHasUnsupportedScheme()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "ftp://localhost:5001",
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ftp");
		result.FailureMessage.ShouldContain("not supported");
	}

	#endregion

	#region MessageSize Failures

	[Fact]
	public void Fail_WhenMaxSendMessageSizeIsZero()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			MaxSendMessageSize = 0,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxSendMessageSize");
	}

	[Fact]
	public void Fail_WhenMaxSendMessageSizeIsNegative()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			MaxSendMessageSize = -1,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxSendMessageSize");
	}

	[Fact]
	public void Fail_WhenMaxReceiveMessageSizeIsZero()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			MaxReceiveMessageSize = 0,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxReceiveMessageSize");
	}

	[Fact]
	public void Fail_WhenMaxReceiveMessageSizeIsNegative()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			MaxReceiveMessageSize = -1,
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("MaxReceiveMessageSize");
	}

	#endregion

	#region Null Guard

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	#endregion
}
