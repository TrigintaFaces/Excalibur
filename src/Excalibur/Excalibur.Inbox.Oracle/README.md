# Excalibur.Inbox.Oracle

Oracle Database implementation of the inbox pattern for idempotent message processing.

## Part Of

The Excalibur application framework — messaging durability providers. Sibling of
`Excalibur.Inbox.SqlServer` and `Excalibur.Inbox.Postgres`.

## Usage

```csharp
services.AddOracleInboxStore(options =>
{
    options.ConnectionString = "User Id=app;Password=***;Data Source=localhost:1521/FREEPDB1";
    options.TableName = "INBOX_MESSAGES";
});
```

The store does not create its table; provision `INBOX_MESSAGES` with the columns the store's
requests reference (a composite primary key of `MessageId, HandlerType`).

## Oracle specifics

- Paging uses ANSI `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY` (Oracle 12c+).
- First-writer-wins deduplication is an `INSERT` guarded by the unique key (`ORA-00001` ⇒ duplicate).
- Concurrent processors of the same key are serialized with `SELECT ... FOR UPDATE`.
- Identity/OCC columns are normalized so Oracle's `'' → NULL` fold cannot defeat a NOT-NULL assertion.
