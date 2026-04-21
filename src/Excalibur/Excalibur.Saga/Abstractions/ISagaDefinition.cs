// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Defines a saga's structure and behavior.
/// </summary>
/// <typeparam name="TSagaData">The type of data flowing through the saga.</typeparam>
/// <remarks>
/// <para>
/// For optional lifecycle callbacks (completion and failure hooks), implement
/// <see cref="ISagaDefinitionLifecycle{TSagaData}"/> alongside this interface.
/// </para>
/// </remarks>
public interface ISagaDefinition<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Gets the saga name.
	/// </summary>
	/// <value>The human-readable saga name.</value>
	string Name { get; }

	/// <summary>
	/// Gets the saga timeout.
	/// </summary>
	/// <value>The maximum time allowed for saga execution.</value>
	TimeSpan Timeout { get; }

	/// <summary>
	/// Gets the steps in this saga.
	/// </summary>
	/// <value>The ordered saga steps.</value>
	IReadOnlyList<ISagaStep<TSagaData>> Steps { get; }

	/// <summary>
	/// Gets the retry policy for the saga.
	/// </summary>
	/// <value>The retry policy to apply when steps fail.</value>
	ISagaRetryPolicy? RetryPolicy { get; }
}
