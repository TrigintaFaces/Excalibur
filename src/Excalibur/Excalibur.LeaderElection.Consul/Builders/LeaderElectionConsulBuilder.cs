// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Consul;

internal sealed class LeaderElectionConsulBuilder : ILeaderElectionConsulBuilder
{
	internal string? AddressValue { get; private set; }
	internal string? TokenValue { get; private set; }
	internal string? DatacenterValue { get; private set; }
	internal TimeSpan? SessionTtlValue { get; private set; }
	internal string? LockKeyValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public ILeaderElectionConsulBuilder Address(string address)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		AddressValue = address;
		BindConfigurationPath = null;
		return this;
	}

	public ILeaderElectionConsulBuilder Token(string token)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(token);
		TokenValue = token;
		return this;
	}

	public ILeaderElectionConsulBuilder Datacenter(string datacenter)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(datacenter);
		DatacenterValue = datacenter;
		return this;
	}

	public ILeaderElectionConsulBuilder SessionTtl(TimeSpan ttl)
	{
		if (ttl <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Session TTL must be positive.");
		}

		SessionTtlValue = ttl;
		return this;
	}

	public ILeaderElectionConsulBuilder LockKey(string lockKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
		LockKeyValue = lockKey;
		return this;
	}

	public ILeaderElectionConsulBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		AddressValue = null;
		return this;
	}
}
