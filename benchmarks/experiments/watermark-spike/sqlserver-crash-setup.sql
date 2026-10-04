CREATE DATABASE WatermarkCrash;
GO
ALTER DATABASE WatermarkCrash SET READ_COMMITTED_SNAPSHOT ON;
GO
USE WatermarkCrash;
CREATE TABLE Ledger(Id int PRIMARY KEY, Payload int NOT NULL, Position rowversion NOT NULL);
CHECKPOINT;
SELECT @@VERSION AS EngineVersion;
SELECT name,is_read_committed_snapshot_on,delayed_durability_desc FROM sys.databases WHERE name=DB_NAME();
GO
