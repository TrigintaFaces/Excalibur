-- Oracle selected timeout hold profile v1. Offline/quiesced migration; no runtime DDL.
-- Edit ONLY the two owner/table literals below for a configured direct physical target.
-- Preserve the existing timeout table and payload; this never truncates/deletes it.
-- Runtime separately checks actual table identity/types; views and synonyms are unsupported.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
DECLARE
    target_owner VARCHAR2(128) := UPPER(DBMS_ASSERT.SCHEMA_NAME('DISPATCH'));
    target_table VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('SAGATIMEOUTS'));
    qualified_target VARCHAR2(300);
    n NUMBER;
    PROCEDURE ensure_column(p_column_name VARCHAR2, p_column_definition VARCHAR2) IS
    BEGIN
        SELECT COUNT(*) INTO n FROM ALL_TAB_COLUMNS
        WHERE OWNER=target_owner AND TABLE_NAME=target_table AND COLUMN_NAME=UPPER(p_column_name);
        IF n=0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE '||qualified_target||' ADD ('||p_column_name||' '||p_column_definition||')';
        END IF;
    END;
    PROCEDURE require_default(p_column_name VARCHAR2, p_expected VARCHAR2) IS
        v_default LONG;
        normalized VARCHAR2(32767);
    BEGIN
        SELECT COUNT(*) INTO n FROM ALL_TAB_COLUMNS WHERE OWNER=target_owner AND TABLE_NAME=target_table AND COLUMN_NAME=UPPER(p_column_name);
        IF n<>0 THEN
            SELECT DATA_DEFAULT INTO v_default FROM ALL_TAB_COLUMNS WHERE OWNER=target_owner AND TABLE_NAME=target_table AND COLUMN_NAME=UPPER(p_column_name);
            normalized := UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(v_default,'(',''),')',''),' ',''),CHR(9),''),CHR(10),''),CHR(13),''));
            IF normalized IS NULL OR normalized<>p_expected THEN
                RAISE_APPLICATION_ERROR(-20131,'Timeout hold default definitions must use SYS_GUID epochs and zero state; existing defaults were not changed.');
            END IF;
        END IF;
    END;
    PROCEDURE ensure_not_null(p_column_name VARCHAR2) IS
    BEGIN
        SELECT COUNT(*) INTO n FROM ALL_TAB_COLUMNS WHERE OWNER=target_owner AND TABLE_NAME=target_table
            AND COLUMN_NAME=UPPER(p_column_name) AND NULLABLE='Y';
        IF n=1 THEN EXECUTE IMMEDIATE 'ALTER TABLE '||qualified_target||' MODIFY ('||p_column_name||' NOT NULL)'; END IF;
    END;
BEGIN
    qualified_target := target_owner||'.'||target_table;
    SELECT COUNT(*) INTO n FROM ALL_TABLES WHERE OWNER=target_owner AND TABLE_NAME=target_table;
    IF n<>1 THEN RAISE_APPLICATION_ERROR(-20131,'Timeout hold profile requires the configured direct physical table.'); END IF;
    -- Refuse incompatible consumer profiles without rewriting their defaults or payload.
    require_default('FailureSourceEpoch','SYS_GUID');
    require_default('FailureDeliveryEpoch','SYS_GUID');
    require_default('FailureHeld','0');
    require_default('FailureVersion','0');
    ensure_column('FailureSourceEpoch','RAW(16) DEFAULT SYS_GUID()');
    ensure_column('FailureDeliveryEpoch','RAW(16) DEFAULT SYS_GUID()');
    ensure_column('FailureHeld','NUMBER(1,0) DEFAULT 0');
    ensure_column('FailureVersion','NUMBER(19,0) DEFAULT 0');
    ensure_column('FailureReceipt','BLOB NULL');
    -- Persist a SINGLE winner for legacy rows. Reads/claims never invent missing epochs.
    -- Quiesce all writers: Oracle DDL commits implicitly before/after statements.
    EXECUTE IMMEDIATE 'LOCK TABLE '||qualified_target||' IN EXCLUSIVE MODE';
    EXECUTE IMMEDIATE 'UPDATE '||qualified_target||' SET FailureSourceEpoch=SYS_GUID() WHERE FailureSourceEpoch IS NULL';
    EXECUTE IMMEDIATE 'UPDATE '||qualified_target||' SET FailureDeliveryEpoch=SYS_GUID() WHERE FailureDeliveryEpoch IS NULL';
    EXECUTE IMMEDIATE 'UPDATE '||qualified_target||' SET FailureHeld=0 WHERE FailureHeld IS NULL';
    EXECUTE IMMEDIATE 'UPDATE '||qualified_target||' SET FailureVersion=0 WHERE FailureVersion IS NULL';
    COMMIT;
    EXECUTE IMMEDIATE 'ALTER TABLE '||qualified_target||' MODIFY (FailureSourceEpoch DEFAULT SYS_GUID(), FailureDeliveryEpoch DEFAULT SYS_GUID(), FailureHeld DEFAULT 0, FailureVersion DEFAULT 0)';
    ensure_not_null('FailureSourceEpoch');
    ensure_not_null('FailureDeliveryEpoch');
    ensure_not_null('FailureHeld');
    ensure_not_null('FailureVersion');
END;
/
