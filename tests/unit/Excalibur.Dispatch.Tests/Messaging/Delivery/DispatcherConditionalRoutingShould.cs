// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Routing.Builder;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Routing")]
public sealed class DispatcherConditionalRoutingShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReevaluateConditionalRoutesForEachMessageAndContext(bool useContext)
	{
		var builder = new RoutingBuilder();
		bool Matches(RoutedEvent message, IMessageContext context) =>
			useContext ? context.Items["tenant"].Equals("B") : message.Premium;
		builder.Transport.Route<RoutedEvent>().When(Matches).To("premium").Default("standard");
		builder.Endpoints.Route<RoutedEvent>().To("common").When(Matches).AlsoTo("private");
		var configuration = new RoutingConfiguration(builder);
		var router = new DefaultDispatchRouter(new ConfiguredTransportSelector(configuration), new ConfiguredEndpointRouter(configuration));
		var terminal = new FinalDispatchHandler(A.Fake<IMessageBusProvider>(), NullLogger<FinalDispatchHandler>.Instance,
			retryPolicy: null, new Dictionary<string, MessageBusOptions>(StringComparer.Ordinal));
		var dispatcher = new Dispatcher(new DispatchMiddlewareInvoker([new ShortCircuit()]), terminal, dispatchRouter: router);

		foreach (var premium in new[] { false, true, false })
		{
			var context = new MessageContext();
			context.Items["tenant"] = premium ? "B" : "A";
			var result = await dispatcher.DispatchAsync(new RoutedEvent(premium), context, CancellationToken.None);
			result.Succeeded.ShouldBeTrue();
			var decision = RoutingDecisionAccessor.GetRoutingDecisionFast(context);
			decision.ShouldNotBeNull();
			decision.Transport.ShouldBe(premium ? "premium" : "standard");
			decision.Endpoints.Contains("private", StringComparer.Ordinal).ShouldBe(premium);
		}
	}

	private sealed record RoutedEvent(bool Premium) : IIntegrationEvent;

	private sealed class ShortCircuit : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IMessageResult>(MessageResult.Success());
	}
}
