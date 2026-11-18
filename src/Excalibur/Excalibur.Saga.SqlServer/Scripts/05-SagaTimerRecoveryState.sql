-- STATE row metadata only; timeout receiver authority is a separate migration.
-- Apply to the configured saga table before the new writer. Legacy NULL incarnation is initialized by version-gated save.
IF COL_LENGTH(N'dispatch.sagas', N'TimerRecoveryIncarnation') IS NULL ALTER TABLE dispatch.sagas ADD TimerRecoveryIncarnation CHAR(32) NULL;
IF COL_LENGTH(N'dispatch.sagas', N'TimerRecoveryTracked') IS NULL ALTER TABLE dispatch.sagas ADD TimerRecoveryTracked BIT NOT NULL CONSTRAINT DF_Sagas_TimerRecoveryTracked DEFAULT 0;
IF COL_LENGTH(N'dispatch.sagas', N'TimerRecoveryPendingBatch') IS NULL ALTER TABLE dispatch.sagas ADD TimerRecoveryPendingBatch CHAR(32) NULL;
IF COL_LENGTH(N'dispatch.sagas', N'TimerRecoveryPendingDigest') IS NULL ALTER TABLE dispatch.sagas ADD TimerRecoveryPendingDigest CHAR(64) NULL;
