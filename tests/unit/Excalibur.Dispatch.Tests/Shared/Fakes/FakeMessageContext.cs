// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.TestFakes;

/// <summary>
///     Fake implementation of IMessageContext for testing purposes.
/// </summary>
public sealed class FakeMessageContext : IMessageContext
{
	private readonly Dictionary<string, object> _items = [];

	public string? MessageId { get; set; }
	public string? CorrelationId { get; set; }
	public string? CausationId { get; set; }
	public IDispatchMessage? Message { get; set; }
	public object? Result { get; set; }

	public IServiceProvider RequestServices { get; set; } = null!;

	public IDictionary<string, object> Items => _items;
	public IDictionary<Type, object> Features { get; } = new Dictionary<Type, object>();

	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

	/// <summary>
	///     Sets the correlation ID for this context.
	/// </summary>
	/// <param name="correlationId"> The correlation ID to set. </param>
	public void SetCorrelationId(Guid correlationId) => CorrelationId = correlationId.ToString();

	/// <summary>
	///     Adds a property to the context.
	/// </summary>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value. </param>
	public void AddProperty(string key, object value) => _items[key] = value;
}
