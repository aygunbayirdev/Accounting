using Accounting.Application.Common.Interfaces;
using Accounting.Application.Services;
using Accounting.Domain.Constants;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Infrastructure.Persistence.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IInvoiceBalanceService invoiceBalanceService,
        IAccountBalanceService accountBalanceService,
        IPasswordHasher passwordHasher,
        CancellationToken ct = default)
    {
        // Helpers (AwayFromZero)
        static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
        static decimal R3(decimal v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);
        static decimal R4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

        // 0) Company Settings
        await SeedCompanySettingsAsync(db, ct);

        // 1) Branches
        await SeedBranchesAsync(db, ct);
        var branchIds = await GetActiveBranchIdsAsync(db, ct);
        var headquartersBranchId = await GetHeadquartersBranchIdAsync(db, ct);

        // 1.5) Roles & Users (Auth için gerekli)
        await SeedRolesAsync(db, ct);
        await SeedUsersAsync(db, passwordHasher, headquartersBranchId, branchIds, ct);

        // 2) Warehouses (per branch default)
        await SeedWarehousesAsync(db, branchIds, ct);
        var warehousesAll = await GetActiveWarehousesAsync(db, ct);
        var defaultWarehouseByBranch = BuildDefaultWarehouseByBranch(warehousesAll);

        // 3) Contacts (Customer/Vendor/Employee)
        await SeedContactsAsync(db, branchIds, ct);

        // 4) Categories
        await SeedCategoriesAsync(db, ct);

        // 5) Items (with categories)
        await SeedItemsAsync(db, branchIds, R4, ct);

        // 6) Cash/Bank Accounts
        await SeedCashBankAccountsAsync(db, branchIds, ct);

        // 7) ExpenseDefinitions
        await SeedExpenseDefinitionsAsync(db, branchIds, ct);

        // Lookup'lar (seed sonrası)
        var contactIds = await db.Contacts.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var customerIds = await db.Contacts.AsNoTracking().Where(x => x.IsCustomer).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var vendorIds = await db.Contacts.AsNoTracking().Where(x => x.IsVendor).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var employeeIds = await db.Contacts.AsNoTracking().Where(x => x.IsEmployee).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);

        var itemsAll = await db.Items.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(ct);
        var accountIds = await db.CashBankAccounts.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);

        var now = DateTime.UtcNow;

        // 8) Stock MVP demo (movements + stocks)
        await SeedStockMovementsAndStocksAsync(
            db,
            branchIds,
            itemsAll,
            defaultWarehouseByBranch,
            now,
            R3,
            ct);

        // 9) Orders
        await SeedOrdersAsync(db, branchIds, itemsAll, customerIds, vendorIds, now, R2, R3, ct);

        // 10) Invoices
        await SeedInvoicesAsync(db, branchIds, itemsAll, customerIds, vendorIds, now, R2, R3, R4, ct);

        // 11) Payments + balance recalc
        await SeedPaymentsAsync(db, branchIds, contactIds, accountIds, invoiceBalanceService, accountBalanceService, now, R2, ct);

        // 12) ExpenseLists
        await SeedExpenseListsAsync(db, branchIds, vendorIds, now, R2, ct);

        // 13) FixedAssets
        await SeedFixedAssetsAsync(db, branchIds, now, R2, R4, ct);

        // 14) Cheques (Çek/Senet)
        await SeedChequesAsync(db, branchIds, customerIds, vendorIds, now, R2, ct);

        await db.SaveChangesAsync(ct);
    }

    // -----------------------------
    // SRP METHODS
    // -----------------------------

    private static async Task SeedCompanySettingsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.CompanySettings.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        var settings = new CompanySettings
        {
            Title = "Demo Şirketi A.Ş.",
            TaxNumber = "1234567890",
            TaxOffice = "Ankara Kurumlar V.D.",
            Address = "Teknokent Bilişim Vadisi, D Blok No:12, Çankaya/ANKARA",
            Phone = "+90 312 555 1234",
            Email = "info@demosirketi.com.tr",
            Website = "https://www.demosirketi.com.tr",
            TradeRegisterNo = "12345",
            MersisNo = "0123456789000015",
            LogoUrl = "https://via.placeholder.com/150",
            CreatedAtUtc = now
        };

        db.CompanySettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedBranchesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Branches.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        db.Branches.AddRange(new List<Branch>
        {
            new() { Code = "MERKEZ", Name = "Merkez Şube", IsHeadquarters = true, CreatedAtUtc = now },
            new() { Code = "ANKARA", Name = "Ankara Şubesi", IsHeadquarters = false, CreatedAtUtc = now },
            new() { Code = "IZMIR",  Name = "İzmir Şubesi", IsHeadquarters = false, CreatedAtUtc = now }
        });

        await db.SaveChangesAsync(ct);
    }
    
    private static async Task<int> GetHeadquartersBranchIdAsync(AppDbContext db, CancellationToken ct)
    {
        var hqBranch = await db.Branches
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsHeadquarters)
            .FirstOrDefaultAsync(ct);
        
        return hqBranch?.Id ?? 1;
    }

    private static async Task<List<int>> GetActiveBranchIdsAsync(AppDbContext db, CancellationToken ct)
    {
        var ids = await db.Branches
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(ct);

        // fallback
        return ids.Count > 0 ? ids : new List<int> { 1 };
    }

    private static async Task SeedWarehousesAsync(AppDbContext db, List<int> branchIds, CancellationToken ct)
    {
        if (await db.Warehouses.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        var list = new List<Warehouse>();
        foreach (var branchId in branchIds)
        {
            list.Add(new Warehouse
            {
                BranchId = branchId,
                Code = "DEPO",
                Name = "Ana Depo",
                IsDefault = true,
                CreatedAtUtc = now
            });
        }

        db.Warehouses.AddRange(list);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<List<Warehouse>> GetActiveWarehousesAsync(AppDbContext db, CancellationToken ct)
    {
        return await db.Warehouses
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private static Dictionary<int, Warehouse> BuildDefaultWarehouseByBranch(List<Warehouse> warehousesAll)
    {
        return warehousesAll
            .GroupBy(w => w.BranchId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).First()
            );
    }

    private static async Task SeedContactsAsync(AppDbContext db, List<int> branchIds, CancellationToken ct)
    {
        if (await db.Contacts.AnyAsync(ct)) return;

        var contacts = new List<Contact>();

        // 1. Şirket Müşteri (Corporate Customer)
        contacts.Add(new Contact
        {
            BranchId = branchIds[0],
            Code = "CUST001",
            Name = "Mavi Teknoloji A.Ş.",
            IsCustomer = true,
            Email = "info@mavitek.com",
            Phone = "02125551010",
            CompanyDetails = new CompanyDetails { TaxNumber = "1111111110", TaxOffice = "Beşiktaş" }
        });

        // 2. Şahıs Müşteri / Perakende (Retail Customer)
        contacts.Add(new Contact
        {
            BranchId = branchIds[1 % branchIds.Count],
            Code = "RET001",
            Name = "Ahmet Yılmaz",
            IsCustomer = false,  // Perakende müşteri, kurumsal değil
            IsRetail = true,
            Email = "ahmet.yilmaz@gmail.com",
            Phone = "05325552020",
            PersonDetails = new PersonDetails { Tckn = "11111111110", FirstName = "Ahmet", LastName = "Yılmaz" }
        });

        // 3. Tedarikçi Şirket (Corporate Vendor)
        contacts.Add(new Contact
        {
            BranchId = branchIds[0],
            Code = "VEND001",
            Name = "Tedarik Gross Ltd.",
            IsVendor = true,
            Email = "satis@tedarikgross.com",
            Phone = "02164443030",
            CompanyDetails = new CompanyDetails { TaxNumber = "2222222220", TaxOffice = "Kadıköy" }
        });

        // 4. Çalışan 1 (Employee)
        contacts.Add(new Contact
        {
            BranchId = branchIds[0],
            Code = "EMP001",
            Name = "Mehmet Öz",
            IsEmployee = true,
            Email = "mehmet.oz@demo.local",
            Phone = "05335554040",
            PersonDetails = new PersonDetails 
            { 
                Tckn = "33333333330", FirstName = "Mehmet", LastName = "Öz", 
                Title = "Satış Müdürü", Department = "Satış" 
            }
        });

        // 5. Çalışan 2 (Employee)
        contacts.Add(new Contact
        {
            BranchId = branchIds[1 % branchIds.Count],
            Code = "EMP002",
            Name = "Ayşe Demir",
            IsEmployee = true,
            Email = "ayse.demir@demo.local",
            Phone = "05345555050",
            PersonDetails = new PersonDetails 
            { 
                Tckn = "44444444440", FirstName = "Ayşe", LastName = "Demir", 
                Title = "Muhasebe Uzmanı", Department = "Finans" 
            }
        });

        // 6. Şirket Müşteri + Tedarikçi (Customer & Vendor) - Mahsuplaşma örneği
        contacts.Add(new Contact
        {
            BranchId = branchIds[2 % branchIds.Count],
            Code = "PARTNER01",
            Name = "Ortak Lojistik A.Ş.",
            IsCustomer = true,
            IsVendor = true,
            Email = "finans@ortaklojistik.com",
            Phone = "08502226060",
            CompanyDetails = new CompanyDetails { TaxNumber = "5555555550", TaxOffice = "Karşıyaka" }
        });

        // 7. Şahıs Şirketi (Sole Proprietorship) - Hem Şahıs hem Şirket
        contacts.Add(new Contact
        {
            BranchId = branchIds[0],
            Code = "SOLE001",
            Name = "Ali Veli - Veli Ticaret",
            IsCustomer = true,
            IsVendor = false,
            Email = "ali.veli@veliticaret.com",
            Phone = "05559998877",
            // Hybrid Identity
            PersonDetails = new PersonDetails 
            { 
                Tckn = "55555555550", 
                FirstName = "Ali", 
                LastName = "Veli" 
            },
            CompanyDetails = new CompanyDetails 
            { 
                TaxNumber = "9999999990", 
                TaxOffice = "Ankara Ostim", 
                MersisNo = "0555555555500001" 
            }
        });

        db.Contacts.AddRange(contacts);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCategoriesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        var categories = new List<Category>
        {
            new() { Name = "Elektronik", Description = "Elektronik ürünler", Color = "#3B82F6" },
            new() { Name = "Gıda", Description = "Gıda ürünleri", Color = "#22C55E" },
            new() { Name = "Kırtasiye", Description = "Kırtasiye malzemeleri", Color = "#F59E0B" },
            new() { Name = "Temizlik", Description = "Temizlik ürünleri", Color = "#8B5CF6" },
            new() { Name = "Hizmet", Description = "Hizmet kalemleri", Color = "#EC4899" }
        };

        db.Categories.AddRange(categories);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedItemsAsync(AppDbContext db, List<int> branchIds, Func<decimal, decimal> R4, CancellationToken ct)
    {
        if (await db.Items.AnyAsync(ct)) return;

        var categoryIds = await db.Categories.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);

        var items = new List<Item>
        {
            new() { BranchId = branchIds[0 % branchIds.Count], CategoryId = categoryIds[0], Code = "LAPTOP01", Name = "Dizüstü Bilgisayar", Unit = "Adet", SalesPrice = R4(15000m), PurchasePrice = R4(12000m), VatRate = 20 },
            new() { BranchId = branchIds[0 % branchIds.Count], CategoryId = categoryIds[0], Code = "MOUSE01", Name = "Kablosuz Mouse", Unit = "Adet", SalesPrice = R4(250m), PurchasePrice = R4(180m), VatRate = 20 },
            new() { BranchId = branchIds[1 % branchIds.Count], CategoryId = categoryIds[1], Code = "KAHVE01", Name = "Filtre Kahve 1kg", Unit = "Kg", SalesPrice = R4(320m), PurchasePrice = R4(240m), VatRate = 10 },
            new() { BranchId = branchIds[1 % branchIds.Count], CategoryId = categoryIds[1], Code = "CAY01", Name = "Siyah Çay 500g", Unit = "Paket", SalesPrice = R4(85m), PurchasePrice = R4(60m), VatRate = 10 },
            new() { BranchId = branchIds[2 % branchIds.Count], CategoryId = categoryIds[2], Code = "KALEM01", Name = "Tükenmez Kalem (12'li)", Unit = "Paket", SalesPrice = R4(48m), PurchasePrice = R4(32m), VatRate = 20 },
            new() { BranchId = branchIds[2 % branchIds.Count], CategoryId = categoryIds[3], Code = "DETERJAN01", Name = "Çamaşır Deterjanı 5L", Unit = "Adet", SalesPrice = R4(180m), PurchasePrice = R4(130m), VatRate = 20 },
            new() { BranchId = branchIds[0 % branchIds.Count], CategoryId = categoryIds[4], Code = "SERVIS01", Name = "Teknik Destek Hizmeti", Unit = "Saat", SalesPrice = R4(500m), PurchasePrice = R4(0m), VatRate = 20 },
            new() { BranchId = branchIds[1 % branchIds.Count], CategoryId = categoryIds[0], Code = "TABLET01", Name = "Tablet 10 inç", Unit = "Adet", SalesPrice = R4(8500m), PurchasePrice = R4(6800m), VatRate = 20 },
            new() { BranchId = branchIds[2 % branchIds.Count], CategoryId = categoryIds[2], Code = "DEFTER01", Name = "Spiralli Defter A4", Unit = "Adet", SalesPrice = R4(35m), PurchasePrice = R4(22m), VatRate = 10 },
            new() { BranchId = branchIds[0 % branchIds.Count], CategoryId = categoryIds[3], Code = "SABUN01", Name = "Sıvı Sabun 5L", Unit = "Adet", SalesPrice = R4(95m), PurchasePrice = R4(65m), VatRate = 10 }
        };

        db.Items.AddRange(items);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCashBankAccountsAsync(AppDbContext db, List<int> branchIds, CancellationToken ct)
    {
        if (await db.CashBankAccounts.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        var accounts = new List<CashBankAccount>
        {
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "KASA01", Name = "Merkez Kasa", Currency = "TRY", Balance = 0m, CreatedAtUtc = now },
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "BANKA01", Name = "İş Bankası TL Hesabı", Currency = "TRY", Balance = 0m, CreatedAtUtc = now },
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "BANKA02", Name = "Garanti USD Hesabı", Currency = "USD", Balance = 0m, CreatedAtUtc = now },
            new() { BranchId = branchIds[1 % branchIds.Count], Code = "KASA02", Name = "Ankara Kasa", Currency = "TRY", Balance = 0m, CreatedAtUtc = now },
            new() { BranchId = branchIds[2 % branchIds.Count], Code = "KASA03", Name = "İzmir Kasa", Currency = "TRY", Balance = 0m, CreatedAtUtc = now }
        };

        db.CashBankAccounts.AddRange(accounts);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedExpenseDefinitionsAsync(AppDbContext db, List<int> branchIds, CancellationToken ct)
    {
        if (await db.ExpenseDefinitions.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        var definitions = new List<ExpenseDefinition>
        {
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "ULASIM", Name = "Ulaşım Giderleri", DefaultVatRate = 20, CreatedAtUtc = now },
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "KIRTASIYE", Name = "Kırtasiye Giderleri", DefaultVatRate = 20, CreatedAtUtc = now },
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "YEMEK", Name = "Yemek Giderleri", DefaultVatRate = 10, CreatedAtUtc = now },
            new() { BranchId = branchIds[0 % branchIds.Count], Code = "KONAKLAMA", Name = "Konaklama Giderleri", DefaultVatRate = 10, CreatedAtUtc = now },
            new() { BranchId = branchIds[1 % branchIds.Count], Code = "YAKIT", Name = "Yakıt Giderleri", DefaultVatRate = 20, CreatedAtUtc = now }
        };

        db.ExpenseDefinitions.AddRange(definitions);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedStockMovementsAndStocksAsync(
        AppDbContext db,
        List<int> branchIds,
        List<Item> itemsAll,
        Dictionary<int, Warehouse> defaultWarehouseByBranch,
        DateTime now,
        Func<decimal, decimal> R3,
        CancellationToken ct)
    {
        if (await db.Stocks.AnyAsync(ct)) return;
        if (!itemsAll.Any()) return;

        var effectiveBranchIds = branchIds.Count > 0 ? branchIds : new List<int> { 1 };

        var movements = new List<StockMovement>();
        var stocks = new List<Stock>();

        var itemsByBranch = itemsAll.GroupBy(i => i.BranchId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var branchId in effectiveBranchIds)
        {
            if (!defaultWarehouseByBranch.TryGetValue(branchId, out var warehouse)) continue;
            if (!itemsByBranch.TryGetValue(branchId, out var branchItems)) continue;

            foreach (var item in branchItems)
            {
                var initialQty = R3(50m + (item.Id * 7) % 100);

                movements.Add(new StockMovement
                {
                    BranchId = branchId,
                    WarehouseId = warehouse.Id,
                    ItemId = item.Id,
                    Type = StockMovementType.AdjustmentIn,
                    Quantity = initialQty,
                    TransactionDateUtc = now.AddDays(-30),
                    Note = "Açılış stoku",
                    CreatedAtUtc = now.AddDays(-30)
                });

                stocks.Add(new Stock
                {
                    BranchId = branchId,
                    WarehouseId = warehouse.Id,
                    ItemId = item.Id,
                    Quantity = initialQty,
                    CreatedAtUtc = now.AddDays(-30)
                });
            }
        }

        db.StockMovements.AddRange(movements);
        db.Stocks.AddRange(stocks);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedOrdersAsync(
        AppDbContext db,
        List<int> branchIds,
        List<Item> itemsAll,
        List<int> customerIds,
        List<int> vendorIds,
        DateTime now,
        Func<decimal, decimal> R2,
        Func<decimal, decimal> R3,
        CancellationToken ct)
    {
        if (await db.Orders.AnyAsync(ct)) return;
        if (!itemsAll.Any()) return;

        var effectiveBranchIds = branchIds.Count > 0 ? branchIds : new List<int> { 1 };

        var orders = new List<Order>();

        // 5 satış siparişi
        for (int i = 1; i <= 5; i++)
        {
            var branchId = effectiveBranchIds[(i - 1) % effectiveBranchIds.Count];
            var customerId = customerIds[(i - 1) % customerIds.Count];
            var item = itemsAll.FirstOrDefault(x => x.BranchId == branchId) ?? itemsAll.First();

            var qty = R3(1m + (i % 5));
            var unitPrice = item.SalesPrice ?? R2(100m);
            var vatRate = item.VatRate;
            var net = R2(qty * unitPrice);
            var vat = R2(net * vatRate / 100m);
            var gross = R2(net + vat);

            var status = i switch
            {
                1 => OrderStatus.Draft,
                2 => OrderStatus.Approved,
                3 => OrderStatus.Invoiced,
                4 => OrderStatus.Cancelled,
                _ => OrderStatus.Draft
            };

            orders.Add(new Order
            {
                BranchId = branchId,
                ContactId = customerId,
                OrderNumber = $"SO-{now.Year}-{i:0000}",
                Type = InvoiceType.Sales,
                Status = status,
                DateUtc = now.AddDays(-i * 2),
                Currency = "TRY",
                TotalNet = net,
                TotalVat = vat,
                TotalGross = gross,
                Lines = new List<OrderLine>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = item.Name,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        Total = net,
                        CreatedAtUtc = now.AddDays(-i * 2)
                    }
                },
                CreatedAtUtc = now.AddDays(-i * 2)
            });
        }

        // 3 alış siparişi
        for (int i = 1; i <= 3; i++)
        {
            var branchId = effectiveBranchIds[(i - 1) % effectiveBranchIds.Count];
            var vendorId = vendorIds[(i - 1) % vendorIds.Count];
            var item = itemsAll.FirstOrDefault(x => x.BranchId == branchId) ?? itemsAll.First();

            var qty = R3(5m + (i * 3));
            var unitPrice = item.PurchasePrice ?? R2(80m);
            var vatRate = item.VatRate;
            var net = R2(qty * unitPrice);
            var vat = R2(net * vatRate / 100m);
            var gross = R2(net + vat);

            orders.Add(new Order
            {
                BranchId = branchId,
                ContactId = vendorId,
                OrderNumber = $"PO-{now.Year}-{i:0000}",
                Type = InvoiceType.Purchase,
                Status = i == 1 ? OrderStatus.Draft : OrderStatus.Approved,
                DateUtc = now.AddDays(-i * 3),
                Currency = "TRY",
                TotalNet = net,
                TotalVat = vat,
                TotalGross = gross,
                Lines = new List<OrderLine>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = item.Name,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        Total = net,
                        CreatedAtUtc = now.AddDays(-i * 3)
                    }
                },
                CreatedAtUtc = now.AddDays(-i * 3)
            });
        }

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedInvoicesAsync(
        AppDbContext db,
        List<int> branchIds,
        List<Item> itemsAll,
        List<int> customerIds,
        List<int> vendorIds,
        DateTime now,
        Func<decimal, decimal> R2,
        Func<decimal, decimal> R3,
        Func<decimal, decimal> R4,
        CancellationToken ct)
    {
        if (await db.Invoices.AnyAsync(ct)) return;
        if (!itemsAll.Any()) return;

        var effectiveBranchIds = branchIds.Count > 0 ? branchIds : new List<int> { 1 };

        var invoices = new List<Invoice>();

        for (int i = 1; i <= 18; i++)
        {
            var isSales = i <= 10;
            var invType = isSales ? InvoiceType.Sales : InvoiceType.Purchase;
            var prefix = isSales ? "SLS" : "PUR";

            var branchId = effectiveBranchIds[(i - 1) % effectiveBranchIds.Count];
            var contactId = isSales
                ? customerIds[(i - 1) % customerIds.Count]
                : vendorIds[(i - 1) % vendorIds.Count];

            var item = itemsAll.FirstOrDefault(x => x.BranchId == branchId) ?? itemsAll.First();

            var qty = R3(1m + (i % 7) * 2);
            var unitPrice = isSales
                ? (item.SalesPrice ?? R4(100m))
                : (item.PurchasePrice ?? R4(80m));

            var vatRate = item.VatRate;
            var net = R2(qty * unitPrice);
            var vat = R2(net * vatRate / 100m);
            var gross = R2(net + vat);

            invoices.Add(new Invoice
            {
                BranchId = branchId,
                ContactId = contactId,
                InvoiceNumber = $"{prefix}-{i:0000}",
                Type = invType,
                DateUtc = now.AddDays(-i),
                Currency = (i % 4 == 0) ? "USD" : "TRY",
                TotalNet = R2(net),
                TotalVat = R2(vat),
                TotalGross = R2(gross),
                Balance = R2(gross),
                Lines = new List<InvoiceLine>
                {
                    new()
                    {
                        ItemId = item.Id,
                        ItemCode = item.Code,
                        ItemName = item.Name,
                        Unit = item.Unit,
                        Qty = qty,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        Net = net,
                        Vat = vat,
                        Gross = gross,
                        CreatedAtUtc = now.AddDays(-i)
                    }
                },
                CreatedAtUtc = now.AddDays(-i)
            });
        }

        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedPaymentsAsync(
        AppDbContext db,
        List<int> branchIds,
        List<int> contactIds,
        List<int> accountIds,
        IInvoiceBalanceService invoiceBalanceService,
        IAccountBalanceService accountBalanceService,
        DateTime now,
        Func<decimal, decimal> R2,
        CancellationToken ct)
    {
        if (await db.Payments.AnyAsync(ct)) return;

        var invoiceIds = await db.Invoices.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);

        var payments = new List<Payment>();

        // Faturaya bağlı ödemeler
        for (int i = 1; i <= Math.Min(10, invoiceIds.Count); i++)
        {
            var invoiceId = invoiceIds[i - 1];

            var invoice = await db.Invoices
                .AsNoTracking()
                .Where(inv => inv.Id == invoiceId)
                .Select(inv => new { inv.TotalGross, inv.Currency, inv.Type })
                .FirstOrDefaultAsync(ct);

            if (invoice == null) continue;
            if (invoice.Type != InvoiceType.Sales && invoice.Type != InvoiceType.Purchase) continue;
            if (invoice.TotalGross <= 0) continue;
            if (invoice.Currency != "TRY") continue;

            var percentage = 0.3m + ((i * 7) % 41) / 100m;
            var paymentAmount = R2(invoice.TotalGross * percentage);

            var accountId = accountIds[(i - 1) % accountIds.Count];
            var contactId = contactIds[(i - 1) % contactIds.Count];

            payments.Add(new Payment
            {
                BranchId = branchIds[(i - 1) % branchIds.Count],
                AccountId = accountId,
                ContactId = contactId,
                LinkedInvoiceId = invoiceId,
                Direction = invoice.Type == InvoiceType.Sales ? PaymentDirection.In : PaymentDirection.Out,
                Amount = paymentAmount,
                Currency = "TRY",
                DateUtc = now.AddHours(-i * 6),
                CreatedAtUtc = now.AddHours(-i * 6)
            });
        }

        // Bağımsız ödemeler
        for (int i = 1; i <= 5; i++)
        {
            var accountId = accountIds[(i - 1) % accountIds.Count];
            var contactId = contactIds[(i - 1) % contactIds.Count];

            payments.Add(new Payment
            {
                BranchId = branchIds[(i - 1) % branchIds.Count],
                AccountId = accountId,
                ContactId = contactId,
                LinkedInvoiceId = null,
                Direction = (i % 2 == 0) ? PaymentDirection.In : PaymentDirection.Out,
                Amount = R2(100m + i * 50m),
                Currency = (i % 3 == 0) ? "USD" : "TRY",
                DateUtc = now.AddHours(-i * 12),
                CreatedAtUtc = now.AddHours(-i * 12)
            });
        }

        db.Payments.AddRange(payments);
        await db.SaveChangesAsync(ct);

        // Invoice balance recalc
        var linkedInvoiceIds = payments
            .Where(p => p.LinkedInvoiceId.HasValue)
            .Select(p => p.LinkedInvoiceId!.Value)
            .Distinct()
            .ToList();

        foreach (var invoiceId in linkedInvoiceIds)
        {
            await invoiceBalanceService.RecalculateBalanceAsync(invoiceId);
        }

        // Account balance recalc
        var affectedAccountIds = payments
            .Select(p => p.AccountId)
            .Distinct()
            .ToList();

        foreach (var accountId in affectedAccountIds)
        {
            await accountBalanceService.RecalculateBalanceAsync(accountId, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedExpenseListsAsync(
        AppDbContext db,
        List<int> branchIds,
        List<int> vendorIds,
        DateTime now,
        Func<decimal, decimal> R2,
        CancellationToken ct)
    {
        if (await db.ExpenseLists.AnyAsync(ct)) return;

        var lists = new List<ExpenseList>();

        for (int i = 1; i <= 10; i++)
        {
            var list = new ExpenseList
            {
                BranchId = branchIds[(i - 1) % branchIds.Count],
                Name = $"Masraf Listesi {i}",
                Status = (i % 3 == 0) ? ExpenseListStatus.Reviewed : ExpenseListStatus.Draft,
                CreatedAtUtc = now.AddDays(-i),
            };

            var supplierId = vendorIds[(i - 1) % vendorIds.Count];
            var amount = R2(50m + i * 12.4m);
            var vatRate = (i % 5 == 0) ? 1 : 20;

            list.Lines.Add(new ExpenseLine
            {
                DateUtc = now.AddDays(-i).AddHours(-2),
                SupplierId = supplierId,
                Currency = (i % 4 == 0) ? "USD" : "TRY",
                Amount = amount,
                VatRate = vatRate,
                Category = (i % 2 == 0) ? "Ulaşım" : "Kırtasiye",
                Notes = (i % 3 == 0) ? "Toplantı gideri" : null,
                CreatedAtUtc = now.AddDays(-i).AddHours(-2)
            });

            lists.Add(list);
        }

        db.ExpenseLists.AddRange(lists);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFixedAssetsAsync(
        AppDbContext db,
        List<int> branchIds,
        DateTime now,
        Func<decimal, decimal> R2,
        Func<decimal, decimal> R4,
        CancellationToken ct)
    {
        if (await db.FixedAssets.AnyAsync(ct)) return;

        var assets = new List<FixedAsset>
        {
            new()
            {
                BranchId = branchIds[0 % branchIds.Count],
                Code = "DMR001",
                Name = "Ofis Bilgisayarı",
                PurchaseDateUtc = now.AddMonths(-18),
                PurchasePrice = R2(25000m),
                UsefulLifeYears = 5,
                DepreciationRatePercent = R4(100m / 5m),
                CreatedAtUtc = now.AddMonths(-18)
            },
            new()
            {
                BranchId = branchIds[1 % branchIds.Count],
                Code = "DMR002",
                Name = "Ofis Mobilyası",
                PurchaseDateUtc = now.AddMonths(-30),
                PurchasePrice = R2(40000m),
                UsefulLifeYears = 8,
                DepreciationRatePercent = R4(100m / 8m),
                CreatedAtUtc = now.AddMonths(-30)
            },
            new()
            {
                BranchId = branchIds[2 % branchIds.Count],
                Code = "DMR003",
                Name = "Yazıcı ve Çevre Donanımı",
                PurchaseDateUtc = now.AddMonths(-6),
                PurchasePrice = R2(12000m),
                UsefulLifeYears = 4,
                DepreciationRatePercent = R4(100m / 4m),
                CreatedAtUtc = now.AddMonths(-6)
            }
        };

        db.FixedAssets.AddRange(assets);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedChequesAsync(
        AppDbContext db,
        List<int> branchIds,
        List<int> customerIds,
        List<int> vendorIds,
        DateTime now,
        Func<decimal, decimal> R2,
        CancellationToken ct)
    {
        if (await db.Cheques.AnyAsync(ct)) return;

        var cheques = new List<Cheque>();
        var bankNames = new[] { "İş Bankası", "Garanti BBVA", "Yapı Kredi", "Ziraat Bankası", "Akbank" };

        // Müşteriden alınan çekler (Inbound)
        for (int i = 1; i <= 6; i++)
        {
            var branchId = branchIds[(i - 1) % branchIds.Count];
            var customerId = customerIds[(i - 1) % customerIds.Count];
            var daysUntilDue = 30 + (i * 15);

            var status = i switch
            {
                1 => ChequeStatus.Paid,
                2 => ChequeStatus.Endorsed,
                6 => ChequeStatus.Bounced,
                _ => ChequeStatus.Pending
            };

            cheques.Add(new Cheque
            {
                BranchId = branchId,
                ContactId = customerId,
                Type = ChequeType.Cheque,
                Direction = ChequeDirection.Inbound,
                Status = status,
                ChequeNumber = $"CHQ-IN-{now.Year}-{i:0000}",
                IssueDate = now.AddDays(-30),
                DueDate = now.AddDays(daysUntilDue - 30),
                Amount = R2(5000m + i * 2500m),
                Currency = "TRY",
                BankName = bankNames[(i - 1) % bankNames.Length],
                BankBranch = $"Şube {i}",
                AccountNumber = $"TR{i:00}0001000{i:00}00000{i:000}",
                DrawerName = $"Müşteri {i} A.Ş.",
                Description = $"Satış bedeli - Fatura grubu {i}",
                CreatedAtUtc = now.AddDays(-30)
            });
        }

        // Müşteriden alınan senetler (Inbound - Promissory Note)
        for (int i = 1; i <= 3; i++)
        {
            var branchId = branchIds[(i - 1) % branchIds.Count];
            var customerId = customerIds[(i - 1) % customerIds.Count];

            cheques.Add(new Cheque
            {
                BranchId = branchId,
                ContactId = customerId,
                Type = ChequeType.PromissoryNote,
                Direction = ChequeDirection.Inbound,
                Status = i == 1 ? ChequeStatus.Paid : ChequeStatus.Pending,
                ChequeNumber = $"SNT-IN-{now.Year}-{i:0000}",
                IssueDate = now.AddDays(-45),
                DueDate = now.AddDays(60 + i * 30),
                Amount = R2(10000m + i * 5000m),
                Currency = "TRY",
                DrawerName = $"Müşteri {i}",
                Description = $"Vadeli satış - Senet {i}",
                CreatedAtUtc = now.AddDays(-45)
            });
        }

        // Tedarikçiye verilen çekler (Outbound)
        for (int i = 1; i <= 4; i++)
        {
            var branchId = branchIds[(i - 1) % branchIds.Count];
            var vendorId = vendorIds[(i - 1) % vendorIds.Count];

            var status = i switch
            {
                1 => ChequeStatus.Paid,
                4 => ChequeStatus.Cancelled,
                _ => ChequeStatus.Pending
            };

            cheques.Add(new Cheque
            {
                BranchId = branchId,
                ContactId = vendorId,
                Type = ChequeType.Cheque,
                Direction = ChequeDirection.Outbound,
                Status = status,
                ChequeNumber = $"CHQ-OUT-{now.Year}-{i:0000}",
                IssueDate = now.AddDays(-15),
                DueDate = now.AddDays(30 + i * 15),
                Amount = R2(3000m + i * 1500m),
                Currency = "TRY",
                BankName = "Şirket Bankası",
                BankBranch = "Merkez",
                AccountNumber = "TR000001000000000001",
                Description = $"Tedarikçi ödemesi - {i}",
                CreatedAtUtc = now.AddDays(-15)
            });
        }

        // Tedarikçiye verilen senetler (Outbound - Promissory Note)
        for (int i = 1; i <= 2; i++)
        {
            var branchId = branchIds[(i - 1) % branchIds.Count];
            var vendorId = vendorIds[(i - 1) % vendorIds.Count];

            cheques.Add(new Cheque
            {
                BranchId = branchId,
                ContactId = vendorId,
                Type = ChequeType.PromissoryNote,
                Direction = ChequeDirection.Outbound,
                Status = ChequeStatus.Pending,
                ChequeNumber = $"SNT-OUT-{now.Year}-{i:0000}",
                IssueDate = now.AddDays(-20),
                DueDate = now.AddDays(90 + i * 30),
                Amount = R2(15000m + i * 7500m),
                Currency = "TRY",
                Description = $"Vadeli alım - Tedarikçi senet {i}",
                CreatedAtUtc = now.AddDays(-20)
            });
        }

        db.Cheques.AddRange(cheques);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Helper to Create Role if not exists
        async Task CreateRoleIfNotExists(string name, string desc, bool isStatic, string[] permissions)
        {
            if (await db.Roles.AnyAsync(r => r.Name == name, ct)) return;

            var role = new Role
            {
                Name = name,
                Description = desc,
                IsStatic = isStatic,
                CreatedAtUtc = now
            };

            foreach (var p in permissions)
            {
                role.Permissions.Add(new RolePermission { Permission = p, CreatedAtUtc = now });
            }

            db.Roles.Add(role);
        }

        // 1. Admin (Sistem Yöneticisi) - Her yetkiye sahip
        await CreateRoleIfNotExists("Admin", "Sistem Yöneticisi - Tam Yetki", true, Permissions.GetAll().ToArray());

        // 2. Patron (İşletme Sahibi) - Her şeyi görür, onaylar, ama sistem ayarlarına dokunmaz (Genelde)
        // KOBİ'de Patron genelde Admin gibidir ama biz ayrım yapalım. CompanySettings okuyup güncelleyebilir.
        var patronPerms = Permissions.GetAll()
            .Where(p => !p.StartsWith("User.") && !p.StartsWith("Role.")) // Kullanıcı yönetimi hariç (opsiyonel)
            .Concat(new[] { Permissions.User.Read, Permissions.Role.Read }) // Sadece görüntülesin
            .ToArray();
        
        // Patron'a Admin gibi full yetki de verebiliriz, talep "Patron" rolü olduğu için business odaklı full yapıyoruz.
        await CreateRoleIfNotExists("Patron", "İşletme Sahibi - Tüm raporlar ve onaylar", false, patronPerms);

        // 3. Muhasebe Şefi (Mali Müşavir / Muhasebe Müdürü)
        // Finansal tüm işlemler, Raporlar, Onaylamalar. Silme yetkisi var.
        var sefPerms = new[]
        {
            // Finans
            Permissions.Invoice.Create, Permissions.Invoice.Read, Permissions.Invoice.Update, Permissions.Invoice.Delete,
            Permissions.Payment.Create, Permissions.Payment.Read, Permissions.Payment.Update, Permissions.Payment.Delete,
            Permissions.CashBankAccount.Create, Permissions.CashBankAccount.Read, Permissions.CashBankAccount.Update, Permissions.CashBankAccount.Delete,
            Permissions.Cheque.Create, Permissions.Cheque.Read, Permissions.Cheque.Update, Permissions.Cheque.Delete, Permissions.Cheque.UpdateStatus,
            // Masraf
            Permissions.ExpenseList.Create, Permissions.ExpenseList.Read, Permissions.ExpenseList.Update, Permissions.ExpenseList.Delete, Permissions.ExpenseList.Review, Permissions.ExpenseList.PostToBill,
            Permissions.ExpenseDefinition.Create, Permissions.ExpenseDefinition.Read, Permissions.ExpenseDefinition.Update, Permissions.ExpenseDefinition.Delete,
            // Demirbaş
            Permissions.FixedAsset.Create, Permissions.FixedAsset.Read, Permissions.FixedAsset.Update, Permissions.FixedAsset.Delete,
            // Ticari
            Permissions.Contact.Create, Permissions.Contact.Read, Permissions.Contact.Update, Permissions.Contact.Delete,
            Permissions.Order.Create, Permissions.Order.Read, Permissions.Order.Update, Permissions.Order.Delete, Permissions.Order.Approve, Permissions.Order.CreateInvoice, Permissions.Order.Cancel,
            Permissions.Item.Create, Permissions.Item.Read, Permissions.Item.Update, Permissions.Item.Delete,
            Permissions.Category.Create, Permissions.Category.Read, Permissions.Category.Update, Permissions.Category.Delete,
            Permissions.Branch.Read,
            Permissions.Warehouse.Read,
            // Rapor
            Permissions.Report.Dashboard, Permissions.Report.ProfitLoss, Permissions.Report.ContactStatement, Permissions.Report.StockStatus,
            // Ayar (Sadece okuma, belki vergi no vs güncelleme)
            Permissions.CompanySettings.Read, Permissions.CompanySettings.Update
        };
        await CreateRoleIfNotExists("MuhasebeSefi", "Muhasebe Şefi - Tam Finansal Yetki", false, sefPerms);

        // 4. Ön Muhasebe (Veri Giriş Elemanı)
        // Fatura/Cari/Sipariş girer. Silemez (Audit). Raporların detayını (Kar/Zarar) görmez.
        var onMuhasebePerms = new[]
        {
            Permissions.Invoice.Create, Permissions.Invoice.Read, Permissions.Invoice.Update, // Delete yok
            Permissions.Payment.Create, Permissions.Payment.Read, Permissions.Payment.Update,
            Permissions.Contact.Create, Permissions.Contact.Read, Permissions.Contact.Update,
            Permissions.Order.Create, Permissions.Order.Read, Permissions.Order.Update, Permissions.Order.CreateInvoice, // Approve/Cancel genelde yetkili işidir
            Permissions.Item.Read,
            Permissions.CashBankAccount.Read, // Bakiyeyi görür ama düzeltme yapamaz
            Permissions.Cheque.Create, Permissions.Cheque.Read, Permissions.Cheque.Update, // Çek girişi yapar
            Permissions.ExpenseList.Create, Permissions.ExpenseList.Read, Permissions.ExpenseList.Update,
            Permissions.Stock.Read,
            Permissions.Warehouse.Read,
            Permissions.Category.Read,
            Permissions.Report.Dashboard // Sadece özet
        };
        await CreateRoleIfNotExists("OnMuhasebe", "Ön Muhasebe - Veri Girişi", false, onMuhasebePerms);

        // 5. Depo Sorumlusu
        // Stok, İrsaliye, Depo. Finans görmez.
        var depoPerms = new[]
        {
            Permissions.Stock.Read, Permissions.Stock.Transfer,
            Permissions.StockMovement.Create, Permissions.StockMovement.Read,
            Permissions.Item.Read, // Ürünleri görür
            Permissions.Warehouse.Read, Permissions.Warehouse.Update,
            Permissions.Order.Read, // Siparişleri görüp hazırlayabilir
            Permissions.Report.StockStatus // Stok durum raporu
        };
        await CreateRoleIfNotExists("DepoSorumlusu", "Depo Sorumlusu - Stok Yönetimi", false, depoPerms);

        // 6. Satış Temsilcisi
        // Sipariş girer, Cari oluşturur. Fatura kesemez (Muhasebeye düşer), Tahsilat yapamaz (veya kısıtlı).
        var satisPerms = new[]
        {
            Permissions.Order.Create, Permissions.Order.Read, Permissions.Order.Update,
            Permissions.Contact.Create, Permissions.Contact.Read, Permissions.Contact.Update, // Müşteri yönetimi
            Permissions.Item.Read, Permissions.Stock.Read, // Ürün ve stok durumu
            Permissions.Report.StockStatus
        };
        await CreateRoleIfNotExists("SatisTemsilcisi", "Satış Temsilcisi - Sipariş ve Cari", false, satisPerms);

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedUsersAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        int headquartersBranchId,
        List<int> branchIds,
        CancellationToken ct)
    {
        // Kullanıcılar var mı kontrolü (basitçe Admin varsa çıkalım, veya daha detaylı bakılabilir)
        if (await db.Users.AnyAsync(u => u.Email == "admin@demo.local", ct)) return;

        var now = DateTime.UtcNow;

        // Rolleri çek
        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin", ct);
        var patronRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Patron", ct) ?? adminRole;
        var sefRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "MuhasebeSefi", ct);
        var onMuhasebeRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "OnMuhasebe", ct);
        var depoRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "DepoSorumlusu", ct);
        var satisRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "SatisTemsilcisi", ct);

        // Eğer yeni roller henüz oluşmadıysa (eski DB), fallback yap
        sefRole ??= adminRole;
        onMuhasebeRole ??= adminRole;
        depoRole ??= adminRole;
        satisRole ??= adminRole;

        var users = new List<User>();
        var userRoles = new List<UserRole>();

        // Helper to add user
        void AddUser(string email, string pass, string first, string last, Role role, int branchId)
        {
            var u = new User
            {
                Email = email,
                PasswordHash = passwordHasher.HashPassword(pass),
                FirstName = first,
                LastName = last,
                IsActive = true,
                BranchId = branchId,
                CreatedAtUtc = now
            };
            users.Add(u);
            
            // id henüz yok, EF Core graph ile eklerken userRoles'u users'a ekleyebiliriz ama 
            // toplu AddRange yapacaksak User nesnelerini önce ekleyip Id almalıyız veya navigation prop kullanmalıyız.
            // Burada navigation prop üzerinden gidelim:
            // u.UserRoles.Add(new UserRole { RoleId = role.Id, CreatedAtUtc = now }); <- UserRole entity constructor/nav prop izin vermeyebilir.
            // Basitlik adına: User'ları ekle, SaveChanges, sonra Rolleri ekle.
        }

        AddUser("admin@demo.local", "Admin123!", "Sistem", "Admin", adminRole, headquartersBranchId);
        AddUser("patron@demo.local", "Patron123!", "Ali", "Patron", patronRole, headquartersBranchId);
        AddUser("sef@demo.local", "Sef123!", "Ayşe", "Müdür", sefRole, headquartersBranchId); // Merkez
        AddUser("muhasebe@demo.local", "Muhasebe123!", "Mehmet", "Hesap", onMuhasebeRole, branchIds.Last()); // Şube
        AddUser("depo@demo.local", "Depo123!", "Veli", "Depocu", depoRole, headquartersBranchId);
        AddUser("satis@demo.local", "Satis123!", "Selin", "Satıcı", satisRole, headquartersBranchId);

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        // Şimdi rolleri ata
        foreach (var u in users)
        {
            Role r = adminRole;
            if (u.Email.StartsWith("patron")) r = patronRole;
            else if (u.Email.StartsWith("sef")) r = sefRole;
            else if (u.Email.StartsWith("muhasebe")) r = onMuhasebeRole;
            else if (u.Email.StartsWith("depo")) r = depoRole;
            else if (u.Email.StartsWith("satis")) r = satisRole;

            db.Set<UserRole>().Add(new UserRole
            {
                UserId = u.Id,
                RoleId = r.Id,
                CreatedAtUtc = now
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
