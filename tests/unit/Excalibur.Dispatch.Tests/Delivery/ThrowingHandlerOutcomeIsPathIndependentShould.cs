// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// A handler that throws must produce the same observable outcome on every dispatch path.
/// </summary>
/// <remarks>
/// The explicit-context overload takes an ultra-local path when nothing is configured and the full
/// pipeline when something is. If one of those returned a failed result and the other threw, a consumer
/// branching on <c>IsSuccess</c> would start seeing unhandled exceptions the moment they removed their
/// last middleware -- a behaviour change with no visible cause.
/// </remarks>
public sealed class ThrowingHandlerOutcomeIsPathIndependentShould
{
	private sealed record ProbeAction : IDispatchAction;

	private sealed class ThrowingHandler : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("handler failed");
	}

	private static ServiceProvider Build(bool withMiddleware)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			if (withMiddleware)
			{
				_ = dispatch.UseValidation();
			}
		});
		_ = services.AddTransient<IActionHandler<ProbeAction>, ThrowingHandler>();

		return services.BuildServiceProvider();
	}

	private static async Task<Exception?> DispatchAsync(bool withMiddleware)
	{
		using var provider = Build(withMiddleware);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var action = new ProbeAction();
		var context = new MessageContext(action, provider);

		try
		{
			var result = await dispatcher.DispatchAsync(action, context, TestContext.Current.CancellationToken);
			result.IsSuccess.ShouldBeFalse("a handler that threw did not succeed");
			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	[Fact]
	public async Task ProduceTheSameOutcomeWithAndWithoutMiddleware()
	{
		var bare = await DispatchAsync(withMiddleware: false);
		var configured = await DispatchAsync(withMiddleware: true);

		// Either both throw or neither does. Which of the two is correct is a contract decision; that
		// they must agree is not. A one-sided change -- making only the configured path throw, or only
		// the bare one return a result -- turns this red.
		(bare is null).ShouldBe(
			configured is null,
			$"bare path: {bare?.GetType().Name ?? "failed result"}; configured path: {configured?.GetType().Name ?? "failed result"}");

		// And they agree on the SAME outcome, not merely on "both threw something": the handler's own
		// exception, unwrapped. Without this, a change that wrapped one side and not the other still passes.
		_ = bare.ShouldBeOfType<InvalidOperationException>();
		bare!.Message.ShouldBe("handler failed");
		_ = configured.ShouldBeOfType<InvalidOperationException>();
		configured!.Message.ShouldBe("handler failed");
	}
}
