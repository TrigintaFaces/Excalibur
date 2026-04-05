// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Provides lifecycle callbacks for saga completion and failure.
/// </summary>
/// <typeparam name="TSagaData">The type of data flowing through the saga.</typeparam>
/// <remarks>
/// <para>
/// Separated from <see cref="ISagaDefinition{TSagaData}"/> following the Interface
/// Segregation Principle. Simple saga definitions that do not need custom completion
/// or failure handling can implement only <see cref="ISagaDefinition{TSagaData}"/>.
/// </para>
/// <para>
/// Implementations that need lifecycle hooks should implement both interfaces.
/// The saga orchestrator checks for this interface at runtime and invokes the
/// callbacks when present.
/// </para>
/// </remarks>
public interface ISagaDefinitionLifecycle<TSagaData>
	where TSagaData : class
{
	/// <summary>
	/// Called when the saga completes successfully.
	/// </summary>
	/// <param name="context">The saga context.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that represents the asynchronous completion callback operation.</returns>
	Task OnCompletedAsync(ISagaContext<TSagaData> context, CancellationToken cancellationToken);

	/// <summary>
	/// Called when the saga fails.
	/// </summary>
	/// <param name="context">The saga context.</param>
	/// <param name="exception">The exception that caused the failure.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that represents the asynchronous failure callback operation.</returns>
	Task OnFailedAsync(ISagaContext<TSagaData> context, Exception exception, CancellationToken cancellationToken);
}
