// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
[Trait("Pattern", "Regression")]
public sealed class PipelineRejectionShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SurfaceRejectionAndReturnContext(bool notification)
	{
		var factory = A.Fake<IMessageContextFactory>();
		var context = new MessageContext();
		A.CallTo(() => factory.CreateContext()).Returns(context);
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch(dispatch => dispatch.UseMiddleware<Reject>());
		services.AddMediatRCompat(config => config.RegisterServicesFromAssembly(typeof(PingHandler).Assembly));
		services.AddSingleton(factory);
		await using var provider = services.BuildServiceProvider();
		var mediator = provider.GetRequiredService<IMediator>();
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			if (notification)
			{
				await mediator.Publish(new Notice());
			}
			else
			{
				await mediator.Send(new Ping());
			}
		});
		exception.Message.ShouldContain("denied");
		A.CallTo(() => factory.Return(context)).MustHaveHappenedOnceExactly();
	}

	internal sealed class Notice : INotification;
	internal sealed class NoticeHandler : INotificationHandler<Notice>
	{
		public Task Handle(Notice notification, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Handler must not run");
	}
	private sealed class Reject : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;
		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IMessageResult>(MessageResult.Failed("denied"));
	}
}
