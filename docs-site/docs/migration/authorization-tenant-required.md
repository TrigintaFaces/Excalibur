---
sidebar_position: 5
title: Authorization Grants Require a Tenant
description: Backfilling grants stored without a tenant on Cosmos DB, DynamoDB, Firestore, and MongoDB before upgrading.
---

# Authorization Grants Require a Tenant

Authorization grants now require a tenant. `Grant.TenantId`, `IActivityGroupGrantStore.InsertActivityGroupGrantAsync`,
and `IActivityGroupStore.CreateActivityGroupAsync` take a non-nullable `string`.

**This page matters to you only if you store grants on Cosmos DB, DynamoDB, Firestore, or MongoDB, and you
have written grants with no tenant.** The SQL Server, PostgreSQL, and in-memory stores never accepted an
absent tenant on their read paths and need no migration.

## What changed on disk

Those four providers previously translated an absent tenant into a reserved literal, and the literal was
part of the **partition key or document id** rather than an ordinary field:

| Provider | Where the literal was written | Value |
|---|---|---|
| Cosmos DB | partition key (`tenant_id`), and inside the document id | `__null__`, and `null` in the id |
| DynamoDB | partition key (`tenant_id`), and inside the index sort key | `__null__`, and `null` in the sort key |
| Firestore | document id **and** the `tenant_id` field | `__null__` |
| MongoDB | inside the `_id` string | `null` |

Because the value is part of a key, correcting it is a **delete and reinsert**, not an update: partition
keys and document ids are immutable on all four.

## Before you upgrade

**Decide what those grants should say**, then rewrite them. There is no automatic conversion, because only
you can know which tenant an untenanted grant belongs to. Two honest options:

1. **Assign them to a real tenant.** For a single-tenant deployment this is usually one tenant identifier
   applied to every affected row.
2. **Delete them.** If they were written by accident, they were never reachable through the tenant-scoped
   read path, and removing them loses nothing you could retrieve.

## Finding the affected rows

Query for the reserved literal before you upgrade — afterwards these rows are still present, but their
tenant reads back as the literal string rather than as an absent value.

**Each provider stores grants in two places, and both must be checked.** Activity-group grants live in
their own container, so a clean grants container tells you nothing about them. These are the default
names; if you set the container names yourself, substitute yours.

| Provider | Grants | Activity-group grants | Option type |
|---|---|---|---|
| Cosmos DB | `grants` | `activity-groups` | `CosmosDbAuthorizationOptions.GrantsContainerName`, `.ActivityGroupsContainerName` |
| DynamoDB | `authorization_grants` | `authorization_activity_groups` | `DynamoDbAuthorizationOptions.GrantsTableName`, `.ActivityGroupsTableName` |
| Firestore | `authorization_grants` | `authorization_activity_groups` | `FirestoreAuthorizationOptions.GrantsCollectionName`, `.ActivityGroupsCollectionName` |
| MongoDB | `grants` | `activity_groups` | `MongoDbAuthorizationOptions.GrantsCollectionName`, `.ActivityGroupsCollectionName` |

:::warning An empty result is not the same as a clean result
On Firestore and MongoDB a collection that does not exist is not an error — querying one returns nothing,
so a mistyped name reads exactly like having no affected rows. Cosmos DB and DynamoDB do fail on a missing
container or table, but a name that exists and is simply the wrong one returns empty on all four.
**Check each name against your own configuration before you read a zero as good news**, and check the
activity-group container separately: a clean grants container tells you nothing about it.
:::

```sql
-- Cosmos DB — run against the grants container, then the activity-groups container
SELECT * FROM c WHERE c.tenant_id = "__null__"
```

```javascript
// MongoDB — the field is already null; the id carries the literal
db.grants.find({ tenantId: null })
db.activity_groups.find({ tenantId: null })
```

For DynamoDB, query both the `authorization_grants` and `authorization_activity_groups` tables with the
partition key `__null__`. For Firestore, query both the `authorization_grants` and
`authorization_activity_groups` collections where the `tenant_id` field equals `__null__`.

## Rewriting

For each affected row: read it, construct the replacement with the chosen tenant, write the replacement,
then delete the original. The replacement lands under a new partition key and a new document id, so the
two coexist until you delete the original — which makes the rewrite resumable if it is interrupted.

Run this **before** deploying the upgraded package. A row left behind is not lost, but it will report the
literal `__null__` as its tenant on Cosmos DB, DynamoDB, and Firestore, and a null tenant on MongoDB.

## After upgrading

An absent tenant is refused at the point it enters the framework rather than translated into a stored
value. An activity-group payload that omits the tenant, and a provisioning request that never named one,
are both rejected with a message naming the entry involved.
