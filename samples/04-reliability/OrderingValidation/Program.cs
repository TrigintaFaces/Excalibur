// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// Ordering Validation Sample
// ==========================
// This sample demonstrates OrderingValidationMiddleware:
// - The receive-to-dispatch bridge stamps the transport's native sequence via
//   TransportOrderingMetadata.TryStampOrdering, simulating a Kafka consumer loop.
// - In-order messages for the same key dispatch successfully.
// - An out-of-order message for the same key throws OutOfOrderMessageException (fail-closed).
// - A message that never entered the marked receive path (an in-process command) passes
//   through unchanged, even though the middleware is registered globally.
//
// Prerequisites:
// 1. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OrderingValidationSample;

var builder = new HostApplicationBuilder(args);

builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch
// ============================================================
// The assembly-scanning AddDispatch(Assembly) overload is used here; the builder-lambda
// AddDispatch(dispatch => { ... }) form composes the same pipeline and works the same way
// with AddOrderingValidation().
builder.Services.AddDispatch(typeof(Program).Assembly);

// AddOrderingValidation() is a service-collection extension, not a dispatch builder one --
// call it alongside AddDispatch(...), not inside its lambda.
builder.Services.AddOrderingValidation();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var dispatcher = host.Services.GetRequiredService<IDispatcher>();

logger.LogInformation("Starting Ordering Validation Sample...");
logger.LogInformation("");

// ============================================================
// Demo 1: In-order messages dispatch successfully
// ============================================================
logger.LogInformation("=== Demo 1: In-Order Delivery ===");
logger.LogInformation("");
logger.LogInformation("Three messages for account ACCT-001, Kafka offsets 10, 11, 12 (strictly increasing):");
logger.LogInformation("");

foreach (var offset in new long[] { 10, 11, 12 })
{
	await DispatchReceivedAsync("ACCT-001", offset, amount: 100m * offset).ConfigureAwait(false);
}

// ============================================================
// Demo 2: Out-of-order message fails closed
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 2: Out-of-Order Delivery (Fail-Closed) ===");
logger.LogInformation("");
logger.LogInformation("Offset 20 then offset 18 for account ACCT-002 -- the second is BEHIND the first:");
logger.LogInformation("");

await DispatchReceivedAsync("ACCT-002", offset: 20, amount: 500m).ConfigureAwait(false);
try
{
	await DispatchReceivedAsync("ACCT-002", offset: 18, amount: 500m).ConfigureAwait(false);
}
catch (OutOfOrderMessageException ex)
{
	logger.LogWarning("Rejected as expected: {Message}", ex.Message);
}

// ============================================================
// Demo 3: Unstamped dispatch passes through unchanged
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 3: In-Process Command (Never Entered a Receive Path) ===");
logger.LogInformation("");
logger.LogInformation("Dispatching directly with no TryStampOrdering call -- the middleware is");
logger.LogInformation("registered globally, but this message was never marked, so it passes through:");
logger.LogInformation("");

var directCommand = new AccountBalanceChanged("ACCT-003", 250m);
var directContext = DispatchContextInitializer.CreateDefaultContext(host.Services);
_ = await dispatcher.DispatchAsync(directCommand, directContext, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("Direct command completed -- ordering validation did not apply to it.");

logger.LogInformation("");
logger.LogInformation("Sample completed.");

// ============================================================
// Helpers
// ============================================================
async Task DispatchReceivedAsync(string accountId, long offset, decimal amount)
{
	// Simulates one message pulled off a Kafka consumer -- ProviderData carries the native
	// partition offset the same way KafkaTransportReceiver populates it in production.
	var received = new TransportReceivedMessage
	{
		Id = Guid.NewGuid().ToString(),
		MessageGroupId = accountId,
		ProviderData = { [TransportOrderingMetadata.KafkaOffsetKey] = offset },
	};

	var context = DispatchContextInitializer.CreateDefaultContext(host.Services);

	// This is the one line a real receive-to-dispatch bridge adds. It reads the offset out of
	// ProviderData, stamps it on the context, and marks the context as having entered an
	// ordered receive path -- which is what scopes OrderingValidationMiddleware to it.
	var stamped = TransportOrderingMetadata.TryStampOrdering(received, context);

	logger.LogInformation(
		"Received {AccountId} @ offset {Offset} (stamped: {Stamped})",
		accountId,
		offset,
		stamped);

	var command = new AccountBalanceChanged(accountId, amount);
	_ = await dispatcher.DispatchAsync(command, context, cancellationToken: default).ConfigureAwait(false);
}

#pragma warning restore CA1303

// ============================================================
// Message and handler
// ============================================================
namespace OrderingValidationSample
{
	public sealed record AccountBalanceChanged(string AccountId, decimal Amount) : IDispatchAction;

	public sealed class AccountBalanceChangedHandler(ILogger<AccountBalanceChangedHandler> logger)
		: IActionHandler<AccountBalanceChanged>
	{
		public Task HandleAsync(AccountBalanceChanged action, CancellationToken cancellationToken)
		{
			logger.LogInformation("  -> Applied balance change of {Amount:C} to {AccountId}", action.Amount, action.AccountId);
			return Task.CompletedTask;
		}
	}
}
