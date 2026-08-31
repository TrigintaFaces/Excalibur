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
