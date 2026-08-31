-- Migration-drift probe: the ORIGINAL body.
--
-- This script is recorded as applied, and the point of the fixture is what happens next.
-- The Edited/ sibling carries the SAME migration id with a DIFFERENT body, which the
-- migrator must refuse. The OriginalCrlf/ sibling carries this body character-for-character
-- with CRLF terminators, which the migrator must accept -- a line-ending translation is what
-- a text=auto checkout does to identical committed content, not a schema change.
--
-- Portable across SQL Server and Postgres on purpose, so both providers bind one fixture.
CREATE TABLE migration_drift_probe (probe_id INT NOT NULL PRIMARY KEY);
