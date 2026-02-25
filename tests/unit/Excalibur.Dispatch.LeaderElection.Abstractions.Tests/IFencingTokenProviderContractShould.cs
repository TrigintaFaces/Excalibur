// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Contract tests for <see cref="IFencingTokenProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IFencingTokenProviderContractShould : UnitTestBase
{
	private readonly IFencingTokenProvider _provider = A.Fake<IFencingTokenProvider>();

	[Fact]
	public async Task GetTokenAsync_ShouldReturnTokenValue()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _provider.GetTokenAsync("resource-1", A<CancellationToken>._))
			.Returns(new ValueTask<long?>(42L));
#pragma warning restore CA2012

		// Act
		var result = await _provider.GetTokenAsync("resource-1", CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task GetTokenAsync_ShouldReturnNull_WhenNoActiveLeader()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _provider.GetTokenAsync("resource-1", A<CancellationToken>._))
			.Returns(new ValueTask<long?>((long?)null));
#pragma warning restore CA2012

		// Act
		var result = await _provider.GetTokenAsync("resource-1", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ValidateTokenAsync_ShouldReturnTrue_ForValidToken()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _provider.ValidateTokenAsync("resource-1", 42L, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));
#pragma warning restore CA2012

		// Act
		var result = await _provider.ValidateTokenAsync("resource-1", 42L, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateTokenAsync_ShouldReturnFalse_ForStaleToken()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _provider.ValidateTokenAsync("resource-1", 10L, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));
#pragma warning restore CA2012

		// Act
		var result = await _provider.ValidateTokenAsync("resource-1", 10L, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IssueTokenAsync_ShouldReturnNewToken()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _provider.IssueTokenAsync("resource-1", A<CancellationToken>._))
			.Returns(new ValueTask<long>(100L));
#pragma warning restore CA2012

		// Act
		var result = await _provider.IssueTokenAsync("resource-1", CancellationToken.None);

		// Assert
		result.ShouldBe(100L);
	}

	[Fact]
	public async Task IssueTokenAsync_ShouldReturnMonotonicallyIncreasingTokens()
	{
		// Arrange
		var callCount = 0L;
#pragma warning disable CA2012
		A.CallTo(() => _provider.IssueTokenAsync("resource-1", A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<long>(Interlocked.Increment(ref callCount)));
#pragma warning restore CA2012

		// Act
		var token1 = await _provider.IssueTokenAsync("resource-1", CancellationToken.None);
		var token2 = await _provider.IssueTokenAsync("resource-1", CancellationToken.None);

		// Assert
		token2.ShouldBeGreaterThan(token1);
	}
}
