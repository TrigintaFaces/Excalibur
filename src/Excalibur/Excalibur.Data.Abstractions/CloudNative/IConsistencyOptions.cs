// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines consistency options for cloud-native database operations.
/// </summary>
public interface IConsistencyOptions
{
	/// <summary>
	/// Gets the desired consistency level for the operation.
	/// </summary>
	ConsistencyLevel ConsistencyLevel { get; }

	/// <summary>
	/// Gets the session token for session-level consistency.
	/// </summary>
	/// <value>
	/// The session token, or null if session consistency is not used.
	/// Session tokens are typically returned from write operations and passed
	/// to subsequent read operations to ensure read-your-writes consistency.
	/// </value>
	string? SessionToken { get; }

	/// <summary>
	/// Gets the maximum staleness for bounded staleness consistency.
	/// </summary>
	/// <value>
	/// The maximum allowed staleness duration, or null if not using bounded staleness.
	/// </value>
	TimeSpan? MaxStaleness { get; }

	/// <summary>
	/// Gets the maximum version lag for bounded staleness consistency.
	/// </summary>
	/// <value>
	/// The maximum number of versions the read can lag behind, or null if not applicable.
	/// </value>
	long? MaxVersionLag { get; }
}

/// <summary>
/// Default implementation of <see cref="IConsistencyOptions"/>.
/// </summary>
public sealed class ConsistencyOptions : IConsistencyOptions
{
	/// <summary>
	/// Gets the default consistency options (uses database default).
	/// </summary>
	public static readonly ConsistencyOptions Default = new(ConsistencyLevel.Default);

	/// <summary>
	/// Gets strong consistency options.
	/// </summary>
	public static readonly ConsistencyOptions Strong = new(ConsistencyLevel.Strong);

	/// <summary>
	/// Gets eventual consistency options.
	/// </summary>
	public static readonly ConsistencyOptions Eventual = new(ConsistencyLevel.Eventual);

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsistencyOptions"/> class.
	/// </summary>
	/// <param name="consistencyLevel">The desired consistency level.</param>
	/// <param name="sessionToken">Optional session token for session consistency.</param>
	/// <param name="maxStaleness">Optional maximum staleness for bounded staleness.</param>
	/// <param name="maxVersionLag">Optional maximum version lag for bounded staleness.</param>
	public ConsistencyOptions(
		ConsistencyLevel consistencyLevel,
		string? sessionToken = null,
		TimeSpan? maxStaleness = null,
		long? maxVersionLag = null)
	{
		ConsistencyLevel = consistencyLevel;
		SessionToken = sessionToken;
		MaxStaleness = maxStaleness;
		MaxVersionLag = maxVersionLag;
	}

	/// <inheritdoc/>
	public ConsistencyLevel ConsistencyLevel { get; }

	/// <inheritdoc/>
	public string? SessionToken { get; }

	/// <inheritdoc/>
	public TimeSpan? MaxStaleness { get; }

	/// <inheritdoc/>
	public long? MaxVersionLag { get; }

	/// <summary>
	/// Creates session consistency options with the specified session token.
	/// </summary>
	/// <param name="sessionToken">The session token from a previous write operation.</param>
	/// <returns>Session consistency options.</returns>
	public static ConsistencyOptions WithSession(string sessionToken) =>
		new(ConsistencyLevel.Session, sessionToken);

	/// <summary>
	/// Creates bounded staleness options with the specified parameters.
	/// </summary>
	/// <param name="maxStaleness">Maximum time staleness allowed.</param>
	/// <param name="maxVersionLag">Maximum version lag allowed (optional).</param>
	/// <returns>Bounded staleness consistency options.</returns>
	public static ConsistencyOptions WithBoundedStaleness(TimeSpan maxStaleness, long? maxVersionLag = null) =>
		new(ConsistencyLevel.BoundedStaleness, maxStaleness: maxStaleness, maxVersionLag: maxVersionLag);
}
