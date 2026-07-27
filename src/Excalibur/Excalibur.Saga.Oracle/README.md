# Excalibur.Saga.Oracle

Oracle Database implementation of the Excalibur saga store and saga timeout store.

Provides durable saga state persistence with optimistic-concurrency control (numeric `Version`
compare-and-swap) using Oracle as the backing store, mirroring `Excalibur.Saga.SqlServer`.

## Schema

The saga timeout store queries `DISPATCH.SAGATIMEOUTS`. Create it before first use — the store does
not create it for you. The DDL ships inside this package under `scripts/`:

```
scripts/SagaTimeouts.sql
```

Run it against your target schema:

```sh
sqlplus user/password@//host:1521/service @SagaTimeouts.sql
```

`DueAt`, `ScheduledAt`, and `ClaimedAt` are `TIMESTAMP WITH TIME ZONE`, not bare `TIMESTAMP`: the store
reads and writes `DateTimeOffset`, and a bare `TIMESTAMP` drops the offset, so the instant would change
across a round trip.

The schema and table names default to `DISPATCH` and `SAGATIMEOUTS` and are configurable through
`OracleSagaTimeoutStoreOptions`. If you change either, edit the script to match before running it.

## Usage

```csharp
services.AddExcalibur(x => x.AddSagas(saga =>
{
    saga.UseOracle(oracle =>
    {
        oracle.ConnectionString(connectionString)
              .SchemaName("DISPATCH")
              .TableName("SAGAS");
    });
}));
```

Oracle folds a zero-length string to `NULL`, so identity columns are normalized to a non-empty
sentinel at the store boundary and paging uses ANSI `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`.
