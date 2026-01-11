-- SET REQUIRED OPTIONS
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- DISABLE FOREIGN KEYS
EXEC sp_msforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all";

-- DELETE DATA (Child First)
IF OBJECT_ID('AuditTrails', 'U') IS NOT NULL DELETE FROM [AuditTrails];
IF OBJECT_ID('RolePermissions', 'U') IS NOT NULL DELETE FROM [RolePermissions];
IF OBJECT_ID('UserRoles', 'U') IS NOT NULL DELETE FROM [UserRoles];
IF OBJECT_ID('StockMovements', 'U') IS NOT NULL DELETE FROM [StockMovements];
IF OBJECT_ID('Stocks', 'U') IS NOT NULL DELETE FROM [Stocks];
IF OBJECT_ID('InvoiceLines', 'U') IS NOT NULL DELETE FROM [InvoiceLines];
IF OBJECT_ID('OrderLines', 'U') IS NOT NULL DELETE FROM [OrderLines];
IF OBJECT_ID('ExpenseLines', 'U') IS NOT NULL DELETE FROM [ExpenseLines];
IF OBJECT_ID('Payments', 'U') IS NOT NULL DELETE FROM [Payments];
IF OBJECT_ID('Cheques', 'U') IS NOT NULL DELETE FROM [Cheques];
IF OBJECT_ID('Invoices', 'U') IS NOT NULL DELETE FROM [Invoices];
IF OBJECT_ID('Orders', 'U') IS NOT NULL DELETE FROM [Orders];
IF OBJECT_ID('ExpenseLists', 'U') IS NOT NULL DELETE FROM [ExpenseLists];
IF OBJECT_ID('Items', 'U') IS NOT NULL DELETE FROM [Items];
IF OBJECT_ID('Warehouses', 'U') IS NOT NULL DELETE FROM [Warehouses];
IF OBJECT_ID('CashBankAccounts', 'U') IS NOT NULL DELETE FROM [CashBankAccounts];
IF OBJECT_ID('ExpenseDefinitions', 'U') IS NOT NULL DELETE FROM [ExpenseDefinitions];
IF OBJECT_ID('FixedAssets', 'U') IS NOT NULL DELETE FROM [FixedAssets];
IF OBJECT_ID('Users', 'U') IS NOT NULL DELETE FROM [Users];
IF OBJECT_ID('Roles', 'U') IS NOT NULL DELETE FROM [Roles];
IF OBJECT_ID('Contacts', 'U') IS NOT NULL DELETE FROM [Contacts];
IF OBJECT_ID('PersonDetails', 'U') IS NOT NULL DELETE FROM [PersonDetails];
IF OBJECT_ID('CompanyDetails', 'U') IS NOT NULL DELETE FROM [CompanyDetails];
IF OBJECT_ID('Categories', 'U') IS NOT NULL DELETE FROM [Categories];
IF OBJECT_ID('Branches', 'U') IS NOT NULL DELETE FROM [Branches];
IF OBJECT_ID('CompanySettings', 'U') IS NOT NULL DELETE FROM [CompanySettings];

-- RESEED IDENTITY
IF OBJECT_ID('AuditTrails', 'U') IS NOT NULL DBCC CHECKIDENT ('[AuditTrails]', RESEED, 0);
IF OBJECT_ID('RolePermissions', 'U') IS NOT NULL DBCC CHECKIDENT ('[RolePermissions]', RESEED, 0);
IF OBJECT_ID('UserRoles', 'U') IS NOT NULL DBCC CHECKIDENT ('[UserRoles]', RESEED, 0);
IF OBJECT_ID('StockMovements', 'U') IS NOT NULL DBCC CHECKIDENT ('[StockMovements]', RESEED, 0);
IF OBJECT_ID('Stocks', 'U') IS NOT NULL DBCC CHECKIDENT ('[Stocks]', RESEED, 0);
IF OBJECT_ID('InvoiceLines', 'U') IS NOT NULL DBCC CHECKIDENT ('[InvoiceLines]', RESEED, 0);
IF OBJECT_ID('OrderLines', 'U') IS NOT NULL DBCC CHECKIDENT ('[OrderLines]', RESEED, 0);
IF OBJECT_ID('ExpenseLines', 'U') IS NOT NULL DBCC CHECKIDENT ('[ExpenseLines]', RESEED, 0);
IF OBJECT_ID('Payments', 'U') IS NOT NULL DBCC CHECKIDENT ('[Payments]', RESEED, 0);
IF OBJECT_ID('Cheques', 'U') IS NOT NULL DBCC CHECKIDENT ('[Cheques]', RESEED, 0);
IF OBJECT_ID('Invoices', 'U') IS NOT NULL DBCC CHECKIDENT ('[Invoices]', RESEED, 0);
IF OBJECT_ID('Orders', 'U') IS NOT NULL DBCC CHECKIDENT ('[Orders]', RESEED, 0);
IF OBJECT_ID('ExpenseLists', 'U') IS NOT NULL DBCC CHECKIDENT ('[ExpenseLists]', RESEED, 0);
IF OBJECT_ID('Items', 'U') IS NOT NULL DBCC CHECKIDENT ('[Items]', RESEED, 0);
IF OBJECT_ID('Warehouses', 'U') IS NOT NULL DBCC CHECKIDENT ('[Warehouses]', RESEED, 0);
IF OBJECT_ID('CashBankAccounts', 'U') IS NOT NULL DBCC CHECKIDENT ('[CashBankAccounts]', RESEED, 0);
IF OBJECT_ID('ExpenseDefinitions', 'U') IS NOT NULL DBCC CHECKIDENT ('[ExpenseDefinitions]', RESEED, 0);
IF OBJECT_ID('FixedAssets', 'U') IS NOT NULL DBCC CHECKIDENT ('[FixedAssets]', RESEED, 0);
IF OBJECT_ID('Users', 'U') IS NOT NULL DBCC CHECKIDENT ('[Users]', RESEED, 0);
IF OBJECT_ID('Roles', 'U') IS NOT NULL DBCC CHECKIDENT ('[Roles]', RESEED, 0);
IF OBJECT_ID('Contacts', 'U') IS NOT NULL DBCC CHECKIDENT ('[Contacts]', RESEED, 0);
IF OBJECT_ID('Categories', 'U') IS NOT NULL DBCC CHECKIDENT ('[Categories]', RESEED, 0);
IF OBJECT_ID('Branches', 'U') IS NOT NULL DBCC CHECKIDENT ('[Branches]', RESEED, 0);
IF OBJECT_ID('CompanySettings', 'U') IS NOT NULL DBCC CHECKIDENT ('[CompanySettings]', RESEED, 0);

-- ENABLE FOREIGN KEYS
EXEC sp_msforeachtable "ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all";