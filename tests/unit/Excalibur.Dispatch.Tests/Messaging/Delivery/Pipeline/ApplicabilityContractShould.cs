// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Pattern", "Regression")]
public sealed class ApplicabilityContractShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task HonorDefaultAndInstanceDependentClassification(bool compiled, bool custom)
	{
		var middleware = new EventOnly();
		IMiddlewareApplicabilityStrategy? strategy = custom ? new Strategy() : null;
		var pipeline = new DispatchPipeline([middleware], strategy);
		var invoker = new DispatchMiddlewareInvoker([middleware], strategy);
		foreach (var selected in new[] { false, true, false })
		{
			var message = new Command(selected);
			if (compiled)
			{
				await invoker.InvokeAsync<IMessageResult>(message, new MessageContext(), Final, CancellationToken.None);
			}
			else
			{
				await pipeline.ExecuteAsync(message, new MessageContext(), Final, CancellationToken.None);
			}
		}
		middleware.Calls.ShouldBe(custom ? 1 : 0);
	}
	private static ValueTask<IMessageResult> Final(IDispatchMessage message, IMessageContext context, CancellationToken token) =>
		ValueTask.FromResult<IMessageResult>(MessageResult.Success());
	private sealed record Command(bool Selected) : IDispatchAction;
	private sealed class Strategy : IMiddlewareApplicabilityStrategy
	{
		public MessageKinds DetermineMessageKinds<T>(T message) where T : IDispatchMessage =>
			((Command)(IDispatchMessage)message).Selected ? MessageKinds.Event : MessageKinds.Action;
		public bool ShouldApplyMiddleware(MessageKinds applicableKinds, MessageKinds messageKinds) => (applicableKinds & messageKinds) != 0;
	}
	private sealed class EventOnly : IDispatchMiddleware
	{
		public int Calls { get; private set; }
		public MessageKinds ApplicableMessageKinds => MessageKinds.Event;
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			Calls++;
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
