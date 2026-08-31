-- SQL Server MIGRATION for Excalibur.Compliance.SqlServer — KEY ESCROW TENANT TOTALITY
-- Version: 1.0
--
-- Closes [compliance].[KeyEscrow].TenantId to NULL, WITHOUT rewriting the term stored in any existing
-- row. Read that sentence again before editing this file: the restraint is the entire point, and the
-- obvious improvement to this script destroys key material.
--
--
-- WHY THIS MIGRATION MAY NOT BACKFILL THE SENTINEL
-- ------------------------------------------------
-- On every other table in this schema the tenant term is a column, and converging it means an UPDATE.
-- Here it is not only a column. The escrow service feeds the same value into the AEAD associated data
-- when it encrypts the key material, and reads it BACK OUT OF THIS COLUMN to rebuild that associated
-- data when it decrypts. The value in the column and the value the ciphertext was authenticated under
-- are therefore required to be equal, and nothing but this column records what that value was.
--
-- So an UPDATE that sets TenantId to '__untenanted__' on an existing row does not migrate it. It
-- changes the associated data out from under a ciphertext that can only be re-authenticated with the
-- master key, which a SQL script does not have. Afterwards the row looks entirely correct — the column
-- holds the value you wanted, the ciphertext is intact, every constraint is satisfied — and the key
-- inside it can never be produced again. The failure surfaces at recovery, which is the one moment
-- escrow exists for, and it surfaces as an authentication-tag mismatch with no indication of the cause.
--
-- That is why the step below moves NULL to the EMPTY STRING and stops there. Absent and empty are the
-- same associated data: the provider writes the term as a length-prefixed field and omits the bytes
-- when the length is zero, so NULL and '' produce byte-identical associated data and the rewrite is
-- inert with respect to every stored ciphertext. It closes the column to NULL, which is what the
-- constraint needs, and it changes nothing a decryption depends on.
--
--
-- WHAT THIS LEAVES BEHIND, STATED RATHER THAN IMPLIED
-- ---------------------------------------------------
-- After this script an untenanted escrow row written before the upgrade holds '', while one written
-- after it holds '__untenanted__'. Two spellings of "no tenant" coexist, which is exactly the
-- ambiguity the totality work removes elsewhere. It is tolerated here, and only here, because both
-- spellings are correct for the rows that carry them and neither can be changed without the master
-- key. It is safe today because TenantId is not a query predicate anywhere in the escrow path: no
-- statement filters on it, so the two spellings never have to compare equal. A future change that
-- introduces such a predicate must match BOTH, and must not "tidy" the older rows into the sentinel.
--
-- Converging the remainder is possible, but not from here. It requires reading each row, decrypting
-- under the term the row currently holds, re-encrypting under the term it should hold, and writing the
-- new ciphertext, IV, authentication tag and term together in one transaction — and for an escrow that
-- has already had recovery tokens generated, re-wrapping its per-batch envelope rows in the same
-- transaction, because their outer layer is authenticated under this same column. That is a tool that
-- runs with the application's key provider, not a script. If you need every row on the sentinel, run
-- such a tool against a restored backup first and verify a recovery from it before touching production.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run with the escrow service stopped, after upgrading the package. Take a backup you have restored at
-- least once — for this table more than any other, since an untested backup of escrowed keys is the
-- same nothing as an untested escrow.
--
-- TenantId participates in no primary key and no unique constraint on this table (the key is EscrowId),
-- so closing it cannot manufacture a uniqueness violation.
--
-- SCOPE: this script targets the DEFAULT object names created by 002. A deployment that configured
-- custom names must rename the objects below to match; the escrow service has no auto-create path that
-- would do it for you.
--
-- Every statement is guarded, so the script is safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'compliance' AND t.name = 'KeyEscrow')
BEGIN
    PRINT 'compliance.KeyEscrow does not exist — nothing to migrate. Provision with 002 first.';
END
ELSE
BEGIN
    DECLARE @IsNullable BIT = (
        SELECT c.is_nullable FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'[compliance].[KeyEscrow]') AND c.name = N'TenantId');

    IF @IsNullable = 0
    BEGIN
        PRINT 'compliance.KeyEscrow.TenantId is already closed to NULL — nothing to do.';
    END
    ELSE
    BEGIN
        DECLARE @Absent INT = (
            SELECT COUNT_BIG(1) FROM [compliance].[KeyEscrow] WHERE TenantId IS NULL);

        -- AAD-neutral by construction: absent and empty are the same zero-length field, so this
        -- rewrite cannot invalidate a stored ciphertext. Anything other than N'' here can.
        UPDATE [compliance].[KeyEscrow] SET TenantId = N'' WHERE TenantId IS NULL;

        ALTER TABLE [compliance].[KeyEscrow]
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        -- The default applies to future writes that omit the column. It does not touch existing rows,
        -- and adding it is the only part of the sentinel that is safe to introduce here.
        IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_KeyEscrow_TenantId')
        BEGIN
            ALTER TABLE [compliance].[KeyEscrow]
                ADD CONSTRAINT DF_KeyEscrow_TenantId DEFAULT N'__untenanted__' FOR TenantId;
        END

        PRINT CONCAT('compliance.KeyEscrow.TenantId closed to NULL. Rows moved from absent to empty: ',
                     @Absent, '.');

        IF @Absent > 0
        BEGIN
            PRINT 'Those rows hold the EMPTY term, not ''__untenanted__''. That is correct and required:';
            PRINT 'their key material is authenticated under the term they were written with, and no';
            PRINT 'SQL statement can change it without destroying them. Do NOT backfill them.';
        END
    END
END
GO
