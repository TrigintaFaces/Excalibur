// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

[Trait("Category", "Unit")]
public sealed class DispatchBuilderSmokeTests
{
	[Fact]
	public async Task Build_ConfiguresDispatcherAndPipeline()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<TestPipelineState>();

		Action<IDispatchBuilder> configure = builder =>
		{
			_ = builder.UseMiddleware<TestMiddleware>();
		};
		_ = services.AddDispatch(configure);

		await using var provider = services.BuildServiceProvider();

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var state = provider.GetRequiredService<TestPipelineState>();

		var context = DispatchContextInitializer.CreateDefaultContext(provider);
		var message = new TestMessage();

		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		Assert.True(result.Succeeded);
		Assert.True(state.WasExecuted);
		Assert.Same(message, context.Message);
	}

	private sealed class TestMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind => MessageKinds.Event;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body { get; } = new { };
		public string MessageType { get; } = nameof(TestMessage);
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestPipelineState
	{
		public bool WasExecuted { get; set; }
	}

	private sealed class TestMiddleware(TestPipelineState state) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			state.WasExecuted = true;
			// Return success directly to verify middleware executed without needing full bus setup
			return new ValueTask<IMessageResult>(Excalibur.Dispatch.Messaging.MessageResult.Success());
		}
	}
}
