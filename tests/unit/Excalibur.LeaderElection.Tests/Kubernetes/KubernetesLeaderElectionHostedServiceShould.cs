// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// Tests for <see cref="KubernetesLeaderElectionHostedService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class KubernetesLeaderElectionHostedServiceShould
{
	private static KubernetesLeaderElectionOptions DisableHealthChecks() => new() { EnableHealthChecks = false };

	[Fact]
	public void ThrowWhenFactoryIsNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new KubernetesLeaderElectionHostedService(null!, "resource", options, null));
	}

	[Fact]
	public void ThrowWhenResourceNameIsNull()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new KubernetesLeaderElectionHostedService(factory, null!, options, null));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new KubernetesLeaderElectionHostedService(factory, "resource", null!, null));
	}

	[Fact]
	public void ConstructSuccessfullyWithValidArgs()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());

		// Act
		using var sut = new KubernetesLeaderElectionHostedService(factory, "test-resource", options, null);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateHealthBasedElectionWhenHealthChecksEnabled()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => factory.CreateHealthBasedElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions
		{
			EnableHealthChecks = true,
		});

		// Act
		using var sut = new KubernetesLeaderElectionHostedService(factory, "test-resource", options, null);

		// Assert
		A.CallTo(() => factory.CreateHealthBasedElection("test-resource", null)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateStandardElectionWhenHealthChecksDisabled()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());

		// Act
		using var sut = new KubernetesLeaderElectionHostedService(factory, "test-resource", options, null);

		// Assert
		A.CallTo(() => factory.CreateElection("test-resource", null)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateStartAsyncToLeaderElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		using var sut = new KubernetesLeaderElectionHostedService(factory, "resource", options, null);

		// Act
		await sut.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => fakeElection.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateStopAsyncToLeaderElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		using var sut = new KubernetesLeaderElectionHostedService(factory, "resource", options, null);

		// Act
		await sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => fakeElection.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ExposeIsLeaderFromUnderlyingElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		A.CallTo(() => fakeElection.IsLeader).Returns(true);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		using var sut = new KubernetesLeaderElectionHostedService(factory, "resource", options, null);

		// Act & Assert
		sut.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public void ExposeCurrentLeaderIdFromUnderlyingElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		A.CallTo(() => fakeElection.CurrentLeaderId).Returns("leader-1");
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		using var sut = new KubernetesLeaderElectionHostedService(factory, "resource", options, null);

		// Act & Assert
		sut.CurrentLeaderId.ShouldBe("leader-1");
	}

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		var sut = new KubernetesLeaderElectionHostedService(factory, "resource", options, null);

		// Act & Assert — should not throw
		Should.NotThrow(() => sut.Dispose());
	}

	[Fact]
	public void DisposeUnderlyingDisposableLeaderElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var fakeElection = A.Fake<ILeaderElection>(x => x.Implements<IDisposable>());
		A.CallTo(() => factory.CreateElection(A<string>._, A<string?>._)).Returns(fakeElection);
		var options = Microsoft.Extensions.Options.Options.Create(DisableHealthChecks());
		var sut = new KubernetesLeaderElectionHostedService(factory, "resource", options, null);

		// Act
		sut.Dispose();

		// Assert
		A.CallTo(() => ((IDisposable)fakeElection).Dispose()).MustHaveHappenedOnceExactly();
	}
}
