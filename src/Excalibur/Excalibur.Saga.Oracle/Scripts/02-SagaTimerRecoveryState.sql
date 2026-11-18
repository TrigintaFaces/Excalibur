-- STATE metadata only; run once against the configured saga table. No automatic migration.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
ALTER TABLE DISPATCH.SAGAS ADD (
    TimerRecoveryIncarnation VARCHAR2(32 CHAR) NULL,
    TimerRecoveryTracked NUMBER(1) DEFAULT 0 NOT NULL,
    TimerRecoveryPendingBatch VARCHAR2(32 CHAR) NULL,
    TimerRecoveryPendingDigest VARCHAR2(64 CHAR) NULL
);
