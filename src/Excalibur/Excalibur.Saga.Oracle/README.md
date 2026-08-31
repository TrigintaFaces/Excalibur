# Excalibur.Saga.Oracle

Oracle Database implementation of the Excalibur saga store and saga timeout store.

Provides durable saga state persistence with optimistic-concurrency control (numeric `Version`
compare-and-swap) using Oracle as the backing store, mirroring `Excalibur.Saga.SqlServer`.

## Schema

This package provisions two independent tables, and neither store creates its table at runtime.
A deployment that uses saga state but not durable timeouts needs only the first.

### Saga state

The saga store reads and writes `DISPATCH.SAGAS`. Create it before the first saga is started:

```
scripts/01-SagaSchema.sql
```

```sh
sqlplus user/password@//host:1521/service @01-SagaSchema.sql
```

Two properties of that table are worth knowing before you provision it:

- **The tenant discriminator is part of the primary key**, which is `(TenantId, SagaId)`. Sagas are
  correlated by a business key such as an order id, so two tenants can legitimately hold the same
  `SagaId`; keyed on `SagaId` alone, one tenant's saga would overwrite the other's. A saga that is
  genuinely not tenant-scoped stores a reserved sentinel, never `NULL` — on Oracle especially, since
  Oracle stores the empty string as `NULL` and a `NULL` tenant is unaddressable by any equality
  predicate.
- **`CompletedAt` drives retention** and is indexed for the purge. It is `TIMESTAMP WITH TIME ZONE`
  while `CreatedUtc` and `UpdatedUtc` are bare `TIMESTAMP`: the latter two are stamped server-side in
  UTC, whereas `CompletedAt` is a consumer-supplied instant whose offset must survive the round trip.

The schema and table names default to `DISPATCH` and `SAGAS` and are configurable through
`OracleSagaStoreOptions`. If you change either, edit the script to match before running it.

### Saga timeouts

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

## Driver license

This package depends on `Oracle.ManagedDataAccess.Core`, Oracle's own ODP.NET Core driver. It is
**not** distributed under an OSI-approved open-source license. Its `LICENSE.txt` opens:

> Your use of this Program is governed by the Oracle Free Distribution, Hosting, and Use Terms and
> Conditions set forth below, unless you have received this Program (alone or as part of another
> Oracle product) under an Oracle license agreement (including but not limited to the Oracle Master
> Agreement), in which case your use of this Program is governed solely by such license agreement
> with Oracle.

Excalibur redistributes no Oracle software and asserts nothing about your eligibility on your
behalf. Referencing this package makes NuGet install the driver into your application, so the
obligations are yours. Read the terms shipped in the driver package before you deploy, and confirm
your deployment is covered -- the free terms carry conditions that the MIT and PostgreSQL licenses
of Excalibur's other database drivers do not.

If those terms do not suit you, no other Excalibur provider carries them: the SQL Server, MySQL and
SQLite drivers are MIT and Npgsql is the PostgreSQL license. Every dependency's license is listed in
`THIRD-PARTY-NOTICES.md` in the repository.
