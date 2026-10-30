-- One row per member lifetime. Advances update only the matching binding.
-- ddl-migration: replacement-and-replay SUBSCRIPTIONCHECKPOINTS docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=tests/integration/Excalibur.Integration.Tests/EventSourcing/Oracle/OracleSubscriptionCheckpointStoreConformanceShould.cs#ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows - Preserve legacy state, provision a new lifetime schema and replay fresh destinations; this CREATE does not upgrade an old table.
-- ddl-migration: retained-legacy SUBSCRIPTIONCHECKPOINTSDEFINITIONS docs=docs-site/docs/migration/checkpoint-schema-upgrade.md test=tests/integration/Excalibur.Integration.Tests/EventSourcing/Oracle/OracleSubscriptionCheckpointStoreConformanceShould.cs#ReplaceLegacyCheckpointTablesWithoutChangingLegacyRows - Retain old definitions unchanged for audit and rollback planning; the current store no longer reads this table.
-- Use binary column collation where enabled; STANDARD databases use binary key constraints
-- and the provider uses explicit binary comparison operators in every key predicate.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

DECLARE
    ddl VARCHAR2(32767) := 'CREATE TABLE EXCALIBUR.SUBSCRIPTIONCHECKPOINTS (
        SUBSCRIPTIONNAME VARCHAR2(255 CHAR) COLLATE BINARY NOT NULL,
        PROJECTIONKEY VARCHAR2(512 CHAR) COLLATE BINARY NOT NULL,
        DEFINITIONHASH VARCHAR2(64 CHAR) COLLATE BINARY NOT NULL,
        DESTINATION VARCHAR2(512 CHAR) COLLATE BINARY NULL,
        DESTINATIONSCOPE VARCHAR2(512 CHAR) COLLATE BINARY NULL,
        BINDINGID VARCHAR2(36 CHAR) COLLATE BINARY NOT NULL UNIQUE,
        POSITION NUMBER(19) NULL CHECK (POSITION IS NULL OR POSITION >= 0),
        PRIMARY KEY (SUBSCRIPTIONNAME, PROJECTIONKEY)
    )';
BEGIN
    EXECUTE IMMEDIATE ddl;
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -43929 THEN
        BEGIN
            EXECUTE IMMEDIATE REPLACE(ddl, ' COLLATE BINARY', '');
        EXCEPTION WHEN OTHERS THEN
            IF SQLCODE != -955 THEN
                RAISE;
            END IF;
        END;
    ELSIF SQLCODE != -955 THEN
        RAISE;
    END IF;
END;
/

DECLARE
    ddl VARCHAR2(32767) := 'CREATE TABLE EXCALIBUR.SUBSCRIPTIONCHECKPOINTSREADERS (
        SUBSCRIPTIONNAME VARCHAR2(255 CHAR) COLLATE BINARY NOT NULL,
        READERNAME VARCHAR2(255 CHAR) COLLATE BINARY NOT NULL,
        REVISION VARCHAR2(36 CHAR) COLLATE BINARY NOT NULL,
        BINDINGID VARCHAR2(36 CHAR) COLLATE BINARY NOT NULL REFERENCES EXCALIBUR.SUBSCRIPTIONCHECKPOINTS(BINDINGID),
        PRIMARY KEY (SUBSCRIPTIONNAME, READERNAME)
    )';
BEGIN
    EXECUTE IMMEDIATE ddl;
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -43929 THEN
        BEGIN
            EXECUTE IMMEDIATE REPLACE(ddl, ' COLLATE BINARY', '');
        EXCEPTION WHEN OTHERS THEN
            IF SQLCODE != -955 THEN
                RAISE;
            END IF;
        END;
    ELSIF SQLCODE != -955 THEN
        RAISE;
    END IF;
END;
/

-- Index the child key so retiring a member does not lock the entire reader table.
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX EXCALIBUR.SUBSCRIPTIONCHECKPOINTSREADERS_BINDING ON EXCALIBUR.SUBSCRIPTIONCHECKPOINTSREADERS(BINDINGID)';
EXCEPTION WHEN OTHERS THEN
    IF SQLCODE != -955 THEN RAISE; END IF;
END;
/
