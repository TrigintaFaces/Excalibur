-- Migration-drift probe: an EDITED body under the SAME migration id.
--
-- A database that ran Original/ is not in the state this script describes, so a migrator
-- that finds this where Original/ was recorded must refuse rather than carry on. This
-- script therefore must never execute; if the drift check regresses, it will, and the
-- extra column below is what makes that visible.
CREATE TABLE migration_drift_probe (probe_id INT NOT NULL PRIMARY KEY, added_later INT NULL);
