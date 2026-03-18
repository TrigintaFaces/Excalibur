---
sidebar_position: 13
title: Transactions
description: How handlers participate in transactions using TransactionMiddleware
---

# Transactions

The `TransactionMiddleware` wraps handler execution in a `System.Transactions.TransactionScope`, providing automatic commit/rollback behavior for command handlers.

## Quick Start

```csharp
// Register connection factory
services.AddSingleton<Func<SqlConnection>>(() => new SqlConnection(connectionString));

// Configure pipeline
services.AddDispatch(dispatch =>
{
    dispatch.UseTransaction();
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

## How It Works

1. `TransactionMiddleware` creates a `TransactionScope` before calling the handler
2. Connections created by `Func<SqlConnection>` auto-enlist in the ambient transaction
3. On handler success, the middleware commits the transaction
4. On exception, the middleware rolls back the transaction

```
TransactionMiddleware [begin TransactionScope]
    |
    v
  Handler
    [DataRequest 1 -- auto-enlists]
    [DataRequest 2 -- auto-enlists]
    |
    v
TransactionMiddleware [commit on success / rollback on exception]
```

## Handler Example

```csharp
public sealed class TransferFundsHandler : IActionHandler<TransferFunds>
{
    private readonly Func<SqlConnection> _connectionFactory;

    public TransferFundsHandler(Func<SqlConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task HandleAsync(TransferFunds command, CancellationToken cancellationToken)
    {
        // Debit source -- auto-enlists in ambient TransactionScope
        using (var connection = _connectionFactory())
        {
            var debit = new DebitAccount(command.FromAccountId, command.Amount);
            await connection.Ready().ResolveAsync(debit).ConfigureAwait(false);
        }

        // Credit destination -- same transaction
        using (var connection = _connectionFactory())
        {
            var credit = new CreditAccount(command.ToAccountId, command.Amount);
            await connection.Ready().ResolveAsync(credit).ConfigureAwait(false);
        }

        // Both operations commit or roll back together
    }
}
```

## Transaction Scope

`TransactionMiddleware` only wraps **Action** messages (commands). Events are read-only notifications and are not wrapped in transactions.

### Configuring Isolation Level

Use the `[Transaction]` attribute on your command to override the default isolation level:

```csharp
[Transaction(IsolationLevel = IsolationLevel.Serializable, TimeoutSeconds = 60)]
public sealed class TransferFunds : IDispatchMessage { }
```

### Bypassing Transactions

Use `[NoTransaction]` to opt out a specific command:

```csharp
[NoTransaction]
public sealed class LogActivity : IDispatchMessage { }
```

## Pipeline Ordering

When combining with other middleware, place `UseTransaction()` after validation but before inbox/outbox:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseSecurityStack();    // Auth first
    dispatch.UseResilienceStack();  // Timeout/retry/circuit breaker
    dispatch.UseValidationStack();  // Validate before transaction
    dispatch.UseTransaction();      // Transaction wraps handler + inbox + outbox
    dispatch.UseInbox();            // Idempotency inside transaction
    dispatch.UseOutbox();           // Outbox inside transaction
});
```

This ensures inbox deduplication and outbox writes participate in the same transaction as the handler's state changes.

## Key Principles

| Principle | Guidance |
|-----------|----------|
| No manual transactions | Do not create `SqlTransaction` manually -- use `TransactionMiddleware` |
| Factory connections | Connections from `Func<SqlConnection>` auto-enlist in `TransactionScope` |
| Atomic handlers | Multiple `DataRequest` operations within a handler are atomic |
| Actions only | Transactions apply to commands, not events |

## See Also

- [Outbox Pattern](./outbox) -- Reliable messaging within transactions
- [Inbox Pattern](./inbox) -- Idempotent processing within transactions
