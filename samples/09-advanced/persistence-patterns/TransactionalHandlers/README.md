# Transactional Handlers Sample

Demonstrates how dispatch handlers participate in transactions using `TransactionMiddleware` and the `Func<SqlConnection>` connection factory pattern.

## How It Works

```
Dispatch(TransferFunds)
     |
     v
TransactionMiddleware [begins TransactionScope]
     |
     v
TransferFundsHandler
  [DebitAccount -- auto-enlists in transaction]
  [CreditAccount -- auto-enlists in transaction]
     |
     v
TransactionMiddleware [commits / rolls back]
```

## Setup

```csharp
// 1. Register connection factory
services.AddSingleton<Func<SqlConnection>>(() => new SqlConnection(connectionString));

// 2. Configure pipeline with transactions
services.AddDispatch(dispatch =>
{
    dispatch.UseTransaction();
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

## Key Principles

- **Do NOT manage SqlTransaction manually** -- use `TransactionMiddleware`
- Connections from the factory **auto-enlist** in the ambient `TransactionScope`
- Multiple `DataRequest` operations within a handler are **atomic**
- `TransactionMiddleware` only applies to **Action** messages (commands), not events

## Files

- `Commands/TransferFunds.cs` -- The command message
- `Requests/DebitAccount.cs` -- DataRequest to debit an account
- `Requests/CreditAccount.cs` -- DataRequest to credit an account
- `Handlers/TransferFundsHandler.cs` -- Handler using connection factory within transaction
