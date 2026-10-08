-- A demo attendance-clock database with the shape TaxiReceipt reads: a
-- Persons table and one punch table per Persian month, named "C" + yyyyMM
-- (e.g. C140507 for Mehr 1405). The real clock system creates each month's
-- table itself; here the current month's is created once.
--
--   sqlcmd -S localhost -E -i db\setup-demo.sql
IF DB_ID(N'TaxiReceiptDemo') IS NULL CREATE DATABASE TaxiReceiptDemo;
GO
USE TaxiReceiptDemo;
GO
IF OBJECT_ID(N'dbo.Persons', N'U') IS NULL
CREATE TABLE dbo.Persons (
    PersonId  int           NOT NULL PRIMARY KEY,
    FirstName nvarchar(50)  NOT NULL,
    LastName  nvarchar(50)  NOT NULL
);
GO
MERGE dbo.Persons AS t
USING (VALUES (1001, N'مریم', N'احمدی'), (1002, N'علی', N'رضایی'), (1003, N'سارا', N'کریمی')) AS s (PersonId, FirstName, LastName)
ON t.PersonId = s.PersonId
WHEN NOT MATCHED THEN INSERT (PersonId, FirstName, LastName) VALUES (s.PersonId, s.FirstName, s.LastName);
GO
-- This month's punch table, named from today's Persian date.
DECLARE @pc nvarchar(6) = FORMAT(SYSDATETIME(), 'yyyyMM', 'fa-IR');
DECLARE @table sysname = N'C' + @pc;
IF OBJECT_ID(N'dbo.' + @table, N'U') IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE TABLE dbo.' + QUOTENAME(@table) + N' (
        Id        bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PersonId  int       NOT NULL,
        PunchTime datetime2 NOT NULL CONSTRAINT DF_' + @table + N'_PunchTime DEFAULT SYSDATETIME()
    );';
    EXEC sp_executesql @sql;
END
PRINT N'Punch table: ' + @table;
GO
