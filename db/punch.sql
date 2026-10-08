-- Simulates the clock: one punch for a person, now, in this month's table.
--   sqlcmd -S localhost -E -d TaxiReceiptDemo -v PersonId=1001 -i db\punch.sql
DECLARE @table sysname = N'C' + FORMAT(SYSDATETIME(), 'yyyyMM', 'fa-IR');
DECLARE @sql nvarchar(max) = N'INSERT INTO dbo.' + QUOTENAME(@table) + N' (PersonId) VALUES (@p);';
EXEC sp_executesql @sql, N'@p int', @p = $(PersonId);
PRINT N'Punched ' + CAST($(PersonId) AS nvarchar(10)) + N' into ' + @table;
