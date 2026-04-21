// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Kubernetes;

internal sealed class LeaderElectionKubernetesBuilder : ILeaderElectionKubernetesBuilder
{
	internal string? NamespaceValue { get; private set; }
	internal string? LeaseNameValue { get; private set; }
	internal string? LeaseIdentityValue { get; private set; }
	internal int? LeaseDurationSeconds { get; private set; }
	internal int? RenewDeadlineMilliseconds { get; private set; }
	internal int? RetryPeriodMilliseconds { get; private set; }
	internal bool UseInCluster { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public ILeaderElectionKubernetesBuilder Namespace(string @namespace)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
		NamespaceValue = @namespace;
		return this;
	}

	public ILeaderElectionKubernetesBuilder LeaseName(string leaseName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(leaseName);
		LeaseNameValue = leaseName;
		return this;
	}

	public ILeaderElectionKubernetesBuilder LeaseIdentity(string identity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(identity);
		LeaseIdentityValue = identity;
		return this;
	}

	public ILeaderElectionKubernetesBuilder LeaseDuration(int seconds)
	{
		if (seconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Lease duration must be positive.");
		}

		LeaseDurationSeconds = seconds;
		return this;
	}

	public ILeaderElectionKubernetesBuilder RenewDeadline(int milliseconds)
	{
		if (milliseconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "Renew deadline must be positive.");
		}

		RenewDeadlineMilliseconds = milliseconds;
		return this;
	}

	public ILeaderElectionKubernetesBuilder RetryPeriod(int milliseconds)
	{
		if (milliseconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "Retry period must be positive.");
		}

		RetryPeriodMilliseconds = milliseconds;
		return this;
	}

	public ILeaderElectionKubernetesBuilder InCluster()
	{
		UseInCluster = true;
		BindConfigurationPath = null;
		return this;
	}

	public ILeaderElectionKubernetesBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		UseInCluster = false;
		return this;
	}
}
