# Ordering Validation Sample

This sample demonstrates **strictly-increasing per-key message ordering** using
`OrderingValidationMiddleware`.

## What This Sample Shows

1. **The receive-to-dispatch bridge** -- calling `TransportOrderingMetadata.TryStampOrdering` to lift a
   transport's native sequence (a Kafka offset here) onto the dispatch context, simulating a real
   consumer loop.
2. **In-order delivery** -- three messages for the same key, strictly increasing offsets, all succeed.
3. **Fail-closed on out-of-order delivery** -- a message with a sequence behind the last one accepted
   for its key throws `OutOfOrderMessageException` and is rejected, not silently processed.
4. **Scope** -- an in-process command that never entered a marked receive path passes through
   unchanged, even with the middleware registered globally.

## Important: Registration Requirement

`AddOrderingValidation()` must be paired with the **assembly-scanning** `AddDispatch(Assembly)`
overload, as this sample does:

```csharp
services.AddDispatch(typeof(Program).Assembly);
services.AddOrderingValidation();
```

Paired instead with the builder-lambda `AddDispatch(dispatch => { ... })` form, the middleware
registers without error but never runs -- no exception, no log, out-of-order messages pass silently.
See the [Ordering Validation](https://excalibur-dispatch.dev/docs/middleware/ordering-validation) docs
page for this limitation in detail.

## Running the Sample

```bash
dotnet run
```

No external infrastructure required -- the transport receive path is simulated in-process.

## Expected Output

```
=== Demo 1: In-Order Delivery ===
Received ACCT-001 @ offset 10 (stamped: True)
  -> Applied balance change of $1,000.00 to ACCT-001
...

=== Demo 2: Out-of-Order Delivery (Fail-Closed) ===
Received ACCT-002 @ offset 20 (stamped: True)
  -> Applied balance change of $500.00 to ACCT-002
Received ACCT-002 @ offset 18 (stamped: True)
Rejected as expected: Message with ordering sequence 18 on key 'ACCT-002' is out of order: ...

=== Demo 3: In-Process Command (Never Entered a Receive Path) ===
  -> Applied balance change of $250.00 to ACCT-003
Direct command completed -- ordering validation did not apply to it.
```

## Key Takeaways

- **Stamping is yours to call.** There is no first-party receive boundary holding both a
  `TransportReceivedMessage` and an `IMessageContext` -- your bridge is the one place holding both.
- **Fail-closed within scope.** A message that entered an ordered receive path but carries no
  resolvable sequence is rejected, never passed silently -- a forgotten stamp surfaces loudly.
- **Registered once, scoped by marking.** The middleware sees every dispatch in the process; what
  bounds it to the messages it's about is the receive-path mark, not where you register it.
