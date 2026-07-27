# Delivery Guarantee Enums

## Two Separate Enums (Intentional)

### `DeliveryGuarantee` (Transport-Level)
- `AtMostOnce` -- Fire and forget
- `AtLeastOnce` -- Retry until acknowledged
- `ExactlyOnce` -- Deduplication + acknowledgment

**Used by:** Transport adapters, message bus configuration. Consumer-facing semantic.

### `OutboxDeliveryGuarantee` (Outbox Processing Strategy)
- `AtLeastOnce` -- Individual message completion
- `MinimizedWindow` -- Mark sent immediately, accept small redelivery window

**Used by:** OutboxProcessor, outbox configuration. Implementation detail.

## Why Not Consolidated

These enums serve fundamentally different purposes:
- `DeliveryGuarantee` = what the consumer promises (messaging semantics)
- `OutboxDeliveryGuarantee` = how the outbox processor achieves it (implementation strategy)

A system can use `DeliveryGuarantee.AtLeastOnce` at the transport level while using `OutboxDeliveryGuarantee.MinimizedWindow` internally.
