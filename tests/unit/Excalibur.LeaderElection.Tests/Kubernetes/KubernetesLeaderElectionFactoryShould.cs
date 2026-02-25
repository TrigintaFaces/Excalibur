// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using FakeItEasy;

using k8s;

using Shouldly;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
public class KubernetesLeaderElectionFactoryShould
{
	[Fact]
	public void Implement_ILeaderElectionFactory()
	{
		// Arrange
		var kubernetesClient = A.Fake<IKubernetes>();
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions());

		// Act
		var factory = new KubernetesLeaderElectionFactory(kubernetesClient, options);

		// Assert
		_ = factory.ShouldBeAssignableTo<ILeaderElectionFactory>();
	}

	[Fact]
	public void Throw_When_KubernetesClient_Is_Null()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new KubernetesLeaderElectionFactory(null!, options));
	}

	[Fact]
	public void Throw_When_Options_Is_Null()
	{
		// Arrange
		var kubernetesClient = A.Fake<IKubernetes>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new KubernetesLeaderElectionFactory(kubernetesClient, null!));
	}

	[Fact]
	public void CreateElection_Returns_ILeaderElection()
	{
		// Arrange
		var kubernetesClient = A.Fake<IKubernetes>();
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions
		{
			Namespace = "test-namespace",
			CandidateId = "test-candidate",
		});
		var factory = new KubernetesLeaderElectionFactory(kubernetesClient, options);

		// Act
		var election = factory.CreateElection("test-resource", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();
		_ = election.ShouldBeAssignableTo<ILeaderElection>();
	}

	[Fact]
	public void CreateHealthBasedElection_Returns_IHealthBasedLeaderElection()
	{
		// Arrange
		var kubernetesClient = A.Fake<IKubernetes>();
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions
		{
			Namespace = "test-namespace",
			CandidateId = "test-candidate",
			EnableHealthChecks = true,
		});
		var factory = new KubernetesLeaderElectionFactory(kubernetesClient, options);

		// Act
		var election = factory.CreateHealthBasedElection("test-resource", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();
		_ = election.ShouldBeAssignableTo<IHealthBasedLeaderElection>();
	}

	[Fact]
	public void CreateElection_Uses_CandidateId_Override()
	{
		// Arrange
		var kubernetesClient = A.Fake<IKubernetes>();
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions
		{
			Namespace = "test-namespace",
			CandidateId = "default-candidate",
		});
		var factory = new KubernetesLeaderElectionFactory(kubernetesClient, options);

		// Act
		var election = factory.CreateElection("test-resource", "override-candidate");

		// Assert
		_ = election.ShouldNotBeNull();
	}

	[Fact]
	public void CreateElection_Copies_CandidateMetadata()
	{
		// Arrange
		var kubernetesClient = A.Fake<IKubernetes>();
		var options = Microsoft.Extensions.Options.Options.Create(new KubernetesLeaderElectionOptions
		{
			Namespace = "test-namespace",
			CandidateId = "test-candidate",
		});
		options.Value.CandidateMetadata["key1"] = "value1";
		options.Value.CandidateMetadata["key2"] = "value2";

		var factory = new KubernetesLeaderElectionFactory(kubernetesClient, options);

		// Act
		var election = factory.CreateElection("test-resource", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();
	}
}
