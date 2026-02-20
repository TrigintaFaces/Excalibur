// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing.Policies;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Policies;

/// <summary>
///     Tests for the <see cref="RoutingPolicyFileLoader" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RoutingPolicyFileLoaderShould : IDisposable
{
	private readonly RoutingPolicyFileLoader _sut;

	public RoutingPolicyFileLoaderShould()
	{
		_sut = new RoutingPolicyFileLoader(
			Microsoft.Extensions.Options.Options.Create(new RoutingPolicyOptions()),
			NullLogger<RoutingPolicyFileLoader>.Instance);
	}

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new RoutingPolicyFileLoader(null!, NullLogger<RoutingPolicyFileLoader>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new RoutingPolicyFileLoader(
				Microsoft.Extensions.Options.Options.Create(new RoutingPolicyOptions()),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveEmptyRulesInitially()
	{
		_sut.Rules.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ReturnEmptyRulesWhenNoPolicyFileConfigured()
	{
		var rules = await _sut.LoadAsync(CancellationToken.None).ConfigureAwait(false);
		rules.Count.ShouldBe(0);
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void DisposeWithoutErrors()
	{
		Should.NotThrow(() => _sut.Dispose());
	}

	public void Dispose() => _sut.Dispose();
}
