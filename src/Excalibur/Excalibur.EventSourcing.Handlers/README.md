# Excalibur.EventSourcing.Handlers

Opt-in integration that routes a dispatched message to an event-sourced aggregate using the
Decider pattern. It bridges Excalibur event sourcing (`IEventSourcedRepository`) and Dispatch
messaging (`IActionHandler`) without pulling messaging concerns into the event-sourcing core.

## Usage

```csharp
services.AddAggregateHandler<OrderAggregate, Guid, PlaceOrder>(
    resolveId: msg => msg.OrderId,
    decide: (order, msg, ct) =>
    {
        order.Place(msg.Items);   // raises domain events via the aggregate
        return Task.CompletedTask;
    });
```

On dispatch the handler:

1. resolves the aggregate id from the message,
2. loads the aggregate (`GetByIdAsync`) — a missing aggregate throws `ResourceNotFoundException`
   (no state is fabricated),
3. invokes `decide`, which raises the aggregate's own events,
4. saves with the load-time ETag as the expected version — a concurrent write throws
   `ConcurrencyException`.

The identity resolver and decision are supplied at registration (not reflected), so registration
is AOT-safe.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
