// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain;

namespace Excalibur.Application;

/// <summary>
/// Adapter that implements IMessageContext by wrapping an IActivityContext.
/// </summary>
/// <remarks>
/// This adapter provides the necessary bridge between the legacy activity context interface and the new message context interface required
/// by Excalibur.Dispatch. The wrapped <see cref="IActivityContext"/> is retained for future extensibility.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="ActivityContextAdapter" /> class. </remarks>
/// <param name="activityContext"> The activity context to wrap. </param>
/// <exception cref="ArgumentNullException"> Thrown when <paramref name="activityContext" /> is null. </exception>
internal sealed class ActivityContextAdapter(IActivityContext activityContext) : IMessageContext
{
	private readonly Dictionary<string, object> _items = [];
	private readonly Dictionary<Type, object> _features = [];

	/// <summary>
	/// Gets the wrapped activity context.
	/// </summary>
	internal IActivityContext ActivityContext { get; } = activityContext ?? throw new ArgumentNullException(nameof(activityContext));

	/// <inheritdoc />
	public string? MessageId { get; set; }

	/// <inheritdoc />
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	public string? CausationId { get; set; }

	/// <inheritdoc />
	public IDispatchMessage? Message { get; set; }

	/// <inheritdoc />
	public object? Result { get; set; }

	/// <inheritdoc />
	public IServiceProvider RequestServices { get; set; } = null!;

	/// <inheritdoc />
	public IDictionary<string, object> Items => _items;

	/// <inheritdoc />
	public IDictionary<Type, object> Features => _features;
}
