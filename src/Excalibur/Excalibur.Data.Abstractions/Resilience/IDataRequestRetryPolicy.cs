// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Core retry policy interface for data providers. Defines retry configuration
/// and transient failure detection without prescribing execution patterns.
/// </summary>
/// <remarks>
/// <para>
/// This is the minimal core interface that ALL data providers implement, including
/// cloud-native providers (DynamoDb, CosmosDb, Firestore) that use their own SDK
/// patterns instead of the <see cref="IDataRequest{TConnection, TResult}"/> pattern.
/// </para>
/// <para>
/// For providers that support the connection-factory execution pattern, use the
/// sub-interfaces:
/// <list type="bullet">
///   <item><see cref="IRelationalDataRequestRetryPolicy"/> for SQL/relational providers</item>
///   <item><see cref="IDocumentDataRequestRetryPolicy"/> for document database providers</item>
/// </list>
/// </para>
/// </remarks>
public interface IDataRequestRetryPolicy
{
	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value> The maximum number of retry attempts. </value>
	int MaxRetryAttempts { get; }

	/// <summary>
	/// Gets the base delay between retry attempts.
	/// </summary>
	/// <value> The base delay between retry attempts. </value>
	TimeSpan BaseRetryDelay { get; }

	/// <summary>
	/// Determines if an exception represents a transient failure that should be retried.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if the exception represents a transient failure; otherwise, false. </returns>
	bool ShouldRetry(Exception exception);
}
