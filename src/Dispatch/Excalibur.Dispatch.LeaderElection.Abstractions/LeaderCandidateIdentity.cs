// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Resolves the candidate identity an election competes under, from the configured instance
/// identifier or, when none was configured, from the environment.
/// </summary>
/// <remarks>
/// <para>
/// This is the single place the identity is established. It is deliberately not per-provider: the
/// candidate identity is the value a lease holder is written from and compared against, so the whole
/// fencing argument is stated over it, and an identity that varies by provider makes that argument
/// provider-specific for no reason.
/// </para>
/// <para>
/// <b>Why the environment is consulted here rather than in one provider.</b> Running in a container
/// is not a property of any particular coordination backend — any provider can run in a pod — so the
/// container's own name for the process belongs in the shared resolver. Reading it here also makes the
/// identity STABLE ACROSS A RESTART wherever the environment supplies one, which the generated
/// fallback cannot be: two processes on one machine with no environment name each receive a fresh
/// random identity, so neither can recognise its own prior tenure.
/// </para>
/// <para>
/// <b>The generated fallback is a last resort and is not stable.</b> It exists so that an election
/// always has an identity to compete under, never as a recommended configuration. A deployment that
/// relies on it cannot distinguish a restarted instance from a new one. The fallback is a complete
/// 32-character GUID without separators; machine-name length cannot truncate its random component.
/// </para>
/// </remarks>
public static class LeaderCandidateIdentity
{
	/// <summary>
	/// The environment variables consulted, in order, when no instance identifier is configured.
	/// </summary>
	/// <remarks>
	/// <c>HOSTNAME</c> is set by container runtimes generally; <c>POD_NAME</c> is the conventional
	/// name when a pod spec projects its own name explicitly.
	/// </remarks>
	private static readonly string[] EnvironmentVariableNames = ["HOSTNAME", "POD_NAME"];

	/// <summary>
	/// Resolves the identity this candidate competes under.
	/// </summary>
	/// <param name="configuredInstanceId">
	/// The identifier the consumer configured, or <see langword="null" /> when they configured none.
	/// Whitespace is treated as unset, because a blank value in configuration is an omission rather
	/// than a choice.
	/// </param>
	/// <returns>
	/// The configured identifier when one was supplied; otherwise the container's own name for this
	/// process; otherwise a generated identifier that is unique but NOT stable across a restart.
	/// </returns>
	public static string Resolve(string? configuredInstanceId)
		=> Resolve(configuredInstanceId, Environment.GetEnvironmentVariable);

	internal static string Resolve(string? configuredInstanceId, Func<string, string?> readEnvironment)
	{
		if (!string.IsNullOrWhiteSpace(configuredInstanceId))
		{
			return configuredInstanceId;
		}

		foreach (var name in EnvironmentVariableNames)
		{
			var fromEnvironment = readEnvironment(name);
			if (!string.IsNullOrWhiteSpace(fromEnvironment))
			{
				return fromEnvironment;
			}
		}

		return Guid.NewGuid().ToString("N");
	}
}
