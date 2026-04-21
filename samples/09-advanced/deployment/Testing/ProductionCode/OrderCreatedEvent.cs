using Excalibur.Dispatch.Abstractions;

namespace Testing.ProductionCode;

/// <summary>
/// Event raised after an order is successfully created.
/// Implements <see cref="IDispatchEvent"/> for the Dispatch event pipeline.
/// </summary>
public sealed record OrderCreatedEvent(string OrderId, string ProductName) : IDispatchEvent;
