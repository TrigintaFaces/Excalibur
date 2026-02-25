// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Polly;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Adapter that bridges Dispatch resilience abstractions with the
/// <see cref="ResiliencePipeline"/> from Microsoft.Extensions.Resilience / Polly v8.
/// </summary>
/// <remarks>
/// <para>
/// This adapter allows consumers who already have a <see cref="ResiliencePipeline"/>
/// configured via <c>Microsoft.Extensions.Resilience</c> to use it directly within
/// the Dispatch resilience framework without reconfiguration.
/// </para>
/// </remarks>
public sealed class DispatchResilienceAdapter
{
	private readonly ResiliencePipeline _pipeline;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchResilienceAdapter"/> class.
	/// </summary>
	/// <param name="pipeline">The Polly v8 resilience pipeline to adapt.</param>
	public DispatchResilienceAdapter(ResiliencePipeline pipeline)
	{
		_pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
	}

	/// <summary>
	/// Executes an operation through the adapted resilience pipeline.
	/// </summary>
	/// <typeparam name="TResult">The type of result returned by the operation.</typeparam>
	/// <param name="operation">The operation to execute.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>The result of the operation.</returns>
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		return await _pipeline.ExecuteAsync(
			async ct => await operation(ct).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Executes a void operation through the adapted resilience pipeline.
	/// </summary>
	/// <param name="operation">The operation to execute.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExecuteAsync(
		Func<CancellationToken, Task> operation,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		await _pipeline.ExecuteAsync(
			async ct =>
			{
				await operation(ct).ConfigureAwait(false);
			},
			cancellationToken).ConfigureAwait(false);
	}
}
