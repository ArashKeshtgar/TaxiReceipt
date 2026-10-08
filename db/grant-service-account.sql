-- Lets the service's virtual account read the clock database, and nothing
-- else: a Windows login for NT SERVICE\TaxiReceipt, a user in the database,
-- and db_datareader. Run once, after install-service.ps1 has created the
-- service (the account doesn't exist before that).
--   sqlcmd -S localhost -E -v Db=TaxiReceiptDemo -i db\grant-service-account.sql
USE master;
IF SUSER_ID(N'NT SERVICE\TaxiReceipt') IS NULL
    CREATE LOGIN [NT SERVICE\TaxiReceipt] FROM WINDOWS;
GO
USE [$(Db)];
IF USER_ID(N'NT SERVICE\TaxiReceipt') IS NULL
    CREATE USER [NT SERVICE\TaxiReceipt] FOR LOGIN [NT SERVICE\TaxiReceipt];
ALTER ROLE db_datareader ADD MEMBER [NT SERVICE\TaxiReceipt];
GO
