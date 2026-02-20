// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Aot.Sample.Handlers;

/// <summary>
/// Handles order creation commands.
/// </summary>
/// <remarks>
/// AOT Pattern:
/// - Handler is discovered at compile time by HandlerRegistrySourceGenerator
/// - No runtime reflection for instantiation (HandlerActivationGenerator)
/// - Direct invocation without dictionary lookup (HandlerInvocationGenerator)
/// </remarks>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
	private readonly IDispatcher _dispatcher;
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="CreateOrderHandler"/> class.
	/// </summary>
	/// <param name="dispatcher">The dispatcher for publishing events.</param>
	/// <param name="serviceProvider">The service provider for creating contexts.</param>
	public CreateOrderHandler(IDispatcher dispatcher, IServiceProvider serviceProvider)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	/// <inheritdoc />
	public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		// Simulate order creation
		var orderId = Guid.NewGuid();
		var totalAmount = action.Items.Sum(item => item.Quantity * item.UnitPrice);

		Console.WriteLine($"[CreateOrderHandler] Creating order {orderId} for customer {action.CustomerId}");
		Console.WriteLine($"[CreateOrderHandler] Items: {action.Items.Count}, Total: ${totalAmount:F2}");

		// Create and dispatch event (also AOT-compatible)
		var orderCreated = new OrderCreatedEvent
		{
			OrderId = orderId,
			CustomerId = action.CustomerId,
			TotalAmount = totalAmount,
			OccurredAt = DateTimeOffset.UtcNow
		};

		// Dispatch event using default context (AOT-safe)
		var context = DispatchContextInitializer.CreateDefaultContext(_serviceProvider);
		_ = await _dispatcher.DispatchAsync(orderCreated, context, cancellationToken).ConfigureAwait(false);

		return orderId;
	}
}
