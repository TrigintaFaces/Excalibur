-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Durable subscription checkpoints.
--
-- A catch-up subscriber reads the global stream in position order and records how far it has processed.
-- Without a durable record of that mark it replays from the beginning on every process start, which for
-- an append-only store means the work grows without bound and every projection is rebuilt on each
-- restart. The gapless global position exists so a subscriber can hold one number and never look back;
-- this table is where that number lives.
--
-- The name is the key, so a checkpoint is per SUBSCRIPTION and not per instance: two instances share one
-- mark and race for it, which the compare-and-set advance in the store resolves. There is deliberately
-- no tenant column -- a subscription reads the global stream of the store it is attached to, and under
-- tenant sharding each shard is a separate database with its own position sequence.

DECLARE
    table_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO table_exists FROM USER_TABLES WHERE TABLE_NAME = 'SUBSCRIPTIONCHECKPOINTS';

    IF table_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE SUBSCRIPTIONCHECKPOINTS (
                SUBSCRIPTIONNAME VARCHAR2(255) NOT NULL,
                POSITION         NUMBER(19)    NOT NULL,
                UPDATEDAT        TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
                CONSTRAINT PK_SUBSCRIPTIONCHECKPOINTS PRIMARY KEY (SUBSCRIPTIONNAME)
            )';
    END IF;
END;
