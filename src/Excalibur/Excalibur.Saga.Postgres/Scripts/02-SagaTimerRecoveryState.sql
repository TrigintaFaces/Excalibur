-- STATE row metadata only; adjust configured identifiers before applying.
ALTER TABLE dispatch.sagas
    ADD COLUMN IF NOT EXISTS timer_recovery_incarnation char(32) NULL,
    ADD COLUMN IF NOT EXISTS timer_recovery_tracked boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS timer_recovery_pending_batch char(32) NULL,
    ADD COLUMN IF NOT EXISTS timer_recovery_pending_digest char(64) NULL;
