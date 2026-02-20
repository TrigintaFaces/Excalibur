// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer.Requests;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class GetCurrentVersionRequestShould
{
	[Fact]
	public void CreateSuccessfully()
	{
		// Act
		var sut = new GetCurrentVersionRequest("agg-1", "Order", null, CancellationToken.None);

		// Assert
		sut.ShouldNotBeNull();
		sut.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenAggregateIdIsInvalid(string? aggregateId)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new GetCurrentVersionRequest(aggregateId!, "Order", null, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenAggregateTypeIsInvalid(string? aggregateType)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new GetCurrentVersionRequest("agg-1", aggregateType!, null, CancellationToken.None));
	}

	[Fact]
	public void AcceptNullTransaction()
	{
		// Act
		var sut = new GetCurrentVersionRequest("agg-1", "Order", null, CancellationToken.None);

		// Assert
		sut.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ExposeResolveAsync()
	{
		// Act
		var sut = new GetCurrentVersionRequest("agg-1", "Order", null, CancellationToken.None);

		// Assert
		sut.ResolveAsync.ShouldNotBeNull();
	}
}
