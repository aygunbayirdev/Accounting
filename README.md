# 📊 Accounting & Inventory Management System

**.NET 8** tabanlı kurumsal muhasebe ve stok yönetimi sistemi. **Clean Architecture**, **CQRS**, **Domain-Driven Design** prensipleriyle geliştirilmiştir.

---

## 🏗️ Mimari

### Katmanlar
```
├── Accounting.Api              # REST API endpoints (Controllers)
├── Accounting.Application      # CQRS (Commands/Queries), Business Logic
├── Accounting.Domain           # Entities, Enums, Value Objects
└── Accounting.Infrastructure   # EF Core, Persistence, External Services
```

### Temel Prensipler
- **CQRS (MediatR)**: Command/Query ayrımı
- **Clean Architecture**: Domain merkezli, bağımlılıklar içe doğru
- **Repository Pattern yok**: CQRS handler'lar direkt `IAppDbContext` kullanır
- **FluentValidation**: Request validation
- **Optimistic Concurrency**: RowVersion ile çakışma kontrolü
- **Soft Delete**: Kayıtlar fiziksel olarak silinmez
- **Audit Trail**: `CreatedAtUtc`, `UpdatedAtUtc` otomatik eklenir

---

## 🔐 Transaction Yönetimi

### Yaklaşım: Manuel Transaction

Projede transaction yönetimi **açık ve görünür** olması için handler'ların içinde manuel olarak yapılmaktadır. Bu sayede:
- Transaction nerede başlıyor/bitiyor açıkça görülür
- Debug ve bakım kolaylaşır
- Junior developer'lar bile kodu kolayca anlayabilir

> **Not:** `TransactionBehavior` ve `ITransactionalRequest` projede mevcut ancak aktif olarak kullanılmıyor. İleride ihtiyaç olursa kullanılabilir.

### Ne Zaman Transaction Gerekli?

| Durum | Örnek | Gerekli mi? |
|-------|-------|-------------|
| **2+ SaveChangesAsync çağrısı** | Payment → InvoiceBalance güncelleme | ✅ EVET |
| **MediatR ile nested command** | PostToBill → CreateInvoice + CreatePayment | ✅ EVET |
| **Tek SaveChangesAsync** | CreateContact, UpdateOrder | ❌ HAYIR |
| **Parent + Child entity (aynı aggregate)** | Order + OrderLines | ❌ HAYIR |

### Ne Zaman Gerekli DEĞİL?

EF Core, tek `SaveChangesAsync()` çağrısını zaten **atomic** olarak çalıştırır:

```csharp
// Bu zaten atomic - Transaction GEREKMEZ
db.Orders.Add(order);
order.Lines.Add(line1);
order.Lines.Add(line2);
await db.SaveChangesAsync(); // Tek çağrı = otomatik transaction
```

### Manuel Transaction Pattern

```csharp
public async Task Handle(CreatePaymentCommand req, CancellationToken ct)
{
    // ... validation ve entity hazırlama ...

    await using var tx = await _db.BeginTransactionAsync(ct);
    try
    {
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        await _balanceService.RecalculateBalanceAsync(invoiceId, ct);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}
```

### Transaction Kullanan Handler'lar

| Handler | Sebep |
|---------|-------|
| `CreatePaymentHandler` | 2x SaveChanges (Payment + InvoiceBalance) |
| `UpdatePaymentHandler` | 2x SaveChanges |
| `SoftDeletePaymentHandler` | 2x SaveChanges |
| `CreateInvoiceHandler` | MediatR.Send (StockMovement) |
| `UpdateInvoiceHandler` | 2x SaveChanges + MediatR.Send |
| `PostExpenseListToBillHandler` | MediatR.Send (Invoice + Payment) |

---

## 🔐 Kimlik Doğrulama & Yetkilendirme

### Kimlik Doğrulama
- **JWT-tabanlı** kimlik doğrulama (access & refresh token)
- **Şifre Hashleme**: `IPasswordHasher` (Identity.Core)
- **Özel** User/Role entity'leri (ASP.NET Identity framework kullanılmıyor)

### Token Claims
```csharp
{
  "id": "5",
  "email": "user@example.com",
  "permission": ["InvoiceCreate", "PaymentView"],
  "role": "Admin",              // Rol bazlı yetkilendirme
  "branchId": "2",              // Şube ataması
  "isHeadquarters": "true"      // Merkez flag
}
```

### Yetkilendirme Stratejileri

#### 1. **Rol Bazlı** (Yönetim İşlemleri)
```csharp
[Authorize(Roles = "Admin")]  // Kullanıcı/Rol yönetimi
public class UsersController : ControllerBase
```

#### 2. **İzin Bazlı** (İş Operasyonları)
```csharp
[RequirePermission("InvoiceCreate")]  // Gelecek: Granular kontrol
```

#### 3. **Şube Bazlı** (Veri İzolasyonu)
Tüm sorgular otomatik olarak şubeye göre filtrelenir (Multi-Branch bölümüne bakınız)

---

## 🏢 Çok Şubeli Veri Görünürlüğü

### Kurallar
- **Admin** kullanıcılar → TÜM şubeleri görebilir
- **Merkez** kullanıcılar → TÜM şubeleri görebilir  
- **Normal** kullanıcılar → SADECE kendi şubelerini görebilir

### Uygulama

#### DRY Extension Method
```csharp
var invoices = await _db.Invoices
    .ApplyBranchFilter(_currentUserService)  // 👈 Tek satır!
    .ToListAsync();
```

#### Ne Yapar?
```csharp
public static IQueryable<T> ApplyBranchFilter<T>(
    this IQueryable<T> query, 
    ICurrentUserService currentUserService) where T : IHasBranch
{
    if (currentUserService.IsAdmin) return query;
    if (currentUserService.IsHeadquarters) return query;
    if (currentUserService.BranchId.HasValue)
        return query.Where(e => e.BranchId == currentUserService.BranchId.Value);
    return query.Where(e => false); // Şube yok = veri yok
}
```

### Güvenlik Garantisi
- ✅ **List handler'lar**: Otomatik filtreleme
- ✅ **GetById handler'lar**: Çapraz şube ID erişimini engeller
- ✅ **`IHasBranch` entity'ler**: Invoice, Payment, Item, Contact, Stock, Warehouse, vb.

### Güvenli Hale Getirilen Entity'ler (18 handler)
**List:** Invoices, Items, Contacts, Payments, ExpenseLists, FixedAssets, CashBankAccounts, Stocks, Warehouses, StockMovements

**GetById:** Invoices, Items, Contacts, Payments, FixedAssets, CashBankAccounts, ExpenseLists, Warehouses

---

## 📦 Domain Modülleri

### 1. **Contacts (Cariler) - Tek Kart Yapısı**
- **Mimari**: Composition Pattern (Hybrid Model)
- **Yapı**: 
  - `Contact`: Ana kimlik ve bayraklar (`IsCustomer`, `IsVendor`, `IsEmployee`, `IsRetail`)
  - `PersonDetails`: Şahıs bilgileri (TCKN, Ad, Soyad) - *Opsiyonel*
  - `CompanyDetails`: Şirket bilgileri (Vergi No, Daire, Mersis) - *Opsiyonel*
- **Esneklik (Hibrid Yapı)**:
  - **Şirket**: Sadece `CompanyDetails` içerir.
  - **Şahıs**: Sadece `PersonDetails` içerir.
  - **Şahıs Şirketi**: Hem `PersonDetails` hem `CompanyDetails` içerir (Tek kartta birleşik).
- **Validasyonlar**:
  - Personel (`IsEmployee`) ise `PersonDetails` zorunludur.
  - Cari kart en az bir detay (Şahıs veya Şirket) içermelidir.
  - Perakende (`IsRetail`) ve Kurumsal (`IsCustomer`) aynı anda olamaz.

### 2. **Items (Ürün/Hizmetler)**
- **Stok ve Hizmet Yönetimi**:
  - `Inventory` (Stoklu Ürün): Stok takibi yapılır, depoya girer/çıkar.
  - `Service` (Hizmet): Stok takibi yapılmaz, sadece faturalanır (Danışmanlık, İşçilik vb.).
- **Özellikler**: CRUD, kod/isim validasyonu, KDV oranı tanımı.

### 3. **Invoices (Faturalar) - KOBİ Standardı**
- **Tipler**: 
  - `Sales` (Satış): Müşteriye kesilen, stoktan düşen (ItemType=Inventory ise).
  - `Purchase` (Alış): Tedarikçiden alınan, stoka giren.
  - `SalesReturn` (Satış İade): Stok geri girer.
  - `PurchaseReturn` (Alış İade): Stok geri çıkar.
- **Kapsamlı Hesaplama**:
  - **Matrah (Net)**: `(Miktar * Fiyat) - İskonto`
  - **İskonto (Discount)**: Satır bazında oran (%) veya tutar.
  - **KDV (VAT)**: Matrah üzerinden hesaplanan vergi.
  - **Tevkifat (Withholding)**: KDV'nin belli oranının (örn. 5/10) alıcı tarafından ödenmesi.
  - **Genel Toplam (Grand Total)**: `Fatura Toplamı - Tevkifat`.
- **Ek Özellikler**: 
  - **İrsaliye Takibi**: İrsaliye No ve Tarihi (`WaybillNumber`, `WaybillDateUtc`).
  - **Vade Takibi**: Ödeme Vade Tarihi (`PaymentDueDateUtc`).
  - **Dövizli Fatura**: Kur (`CurrencyRate`) ve Döviz Cinsi takibi.

#### Tevkifat (Withholding) Detayları
KDV'nin bir kısmının alıcı tarafından kesilip doğrudan vergi dairesine ödenmesidir.

**1. Kapsam (Scope)**
- **Satır Bazlıdır**: Bir faturada hem tevkifatlı (örn. İşçilik) hem tevkifatsız (örn. Malzeme) kalemler aynı anda bulunabilir. Sistem her satırı ayrı hesaplar.
- **Hem Hizmet Hem Stok**: Genellikle hizmet sektöründe (Temizlik, Nakliye) yaygın olsa da, bazı stoklu ürünlerde (Hurda, Bakır, Sunta vb.) de tevkifat zorunluluğu vardır. Sistemimizde `Inventory` veya `Service` fark etmeksizin her kaleme tevkifat uygulanabilir.

**2. Hesaplama Mantığı (Logic)**
Bu sistemde hesaplama şu formülle yapılır:
> **Alacağınız Para (Balance) = (Matrah + KDV) - Tevkifat Tutarı**

_Örnek Senaryo: 1000 TL + %20 KDV (%5/10 Tevkifat)_
- **Matrah (Net)**: 1.000 TL
- **Hesaplanan KDV (%20)**: 200 TL
- **Uygulanan Tevkifat (5/10)**: 100 TL _(Bu tutarı alıcı sizin adınıza devlete öder)_
- **Fatura Brüt Toplamı**: 1.200 TL
- **Cari Hesaba İşleyen (Tahsil Edilecek)**: **1.100 TL** (1200 - 100)

> *Sistemde tevkifat oranını (Rate) girdiğinizde (örn: 50), Tutar (Amount) ve Cari Bakiye (Balance) otomatik hesaplanır.*

### 4. **Payments (Tahsilat/Tediye)**
- **Yönler**: In (Tahsilat), Out (Ödeme)
- **İlişkiler**: CashBankAccount, Contact, Invoice
- **Özellikler**: Multi-currency, date range filtering

### 5. **Expense Lists (Masraf Listeleri)**
- **Workflow**: Draft → Reviewed → Posted
- **Post to Bill**: Masraf listesini satın alma faturasına çevirir
- **Özellikler**: Line-based editing, approval system

### 6. **Stock Management (Stok Yönetimi)**
- **Warehouse**: Depo tanımları
- **Stock**: Anlık stok miktarları (Warehouse + Item bazında)
- **StockMovement**: Stok hareketleri
  - **Tipler**: PurchaseIn, SalesOut, AdjustmentIn, AdjustmentOut

### 7. **Cash/Bank Accounts (Kasa/Banka)**
- **Tipler**: Cash, Bank
- Tahsilat/tediye hesapları

### 8. **Fixed Assets (Demirbaşlar)**
- Sabit kıymet yönetimi (MVP'de henüz aktif değil)

### 9. **Cheques & Promissory Notes (Çek/Senet)**
- **Tipler**: Cheque (Çek), PromissoryNote (Senet)
- **Yönler**: Inbound (Müşteriden alınan), Outbound (Tedarikçiye verilen)
- **Durumlar**: Pending, Paid, Bounced (Karşılıksız), Endorsed (Ciro)
- **Özellikler**: vade takibi, tahsilat/ödeme entegrasyonu.

### 10. **Identity & Access Management (IAM)**
- **Users**: Kullanıcı yönetimi, şifre hashleme, rol atama.
- **Roles**: Dinamik rol ve izin (Permission) yönetimi.
- **Güvenlik**: JWT tabanlı, Branch-scoped erişim kontrolü.

#### Varsayılan Roller (DataSeeder)
Sistem **KOBİ** standartlarına uygun, otomatik oluşturulan hazır rollerle gelir:

| Rol | Açıklama | Tipik Yetkiler | Örnek Kullanıcı (Şifre: ...123!) |
|-----|----------|----------------|-----------------|
| **Admin** | Sistem Yöneticisi | Sistemin **TAMAMINA** tam erişim. | `admin@demo.local` |
| **Patron** | İşletme Sahibi | Tüm raporları ve kayıtları **görür ve onaylar**. Sistem ayarlarına dokunmaz. | `patron@demo.local` |
| **MuhasebeSefi** | Mali Müşavir / Müdür | Tam finansal yetki (Fatura, Çek, Banka, Silme, İade). | `sef@demo.local` |
| **OnMuhasebe** | Muhasebe Elemanı | Günlük veri girişi (Fatura, Cari, Sipariş). **Kayıt SİLEMEZ.** Kâr/Zarar görmez. | `muhasebe@demo.local` |
| **DepoSorumlusu** | Depo Amiri | Sadece Stok, İrsaliye, Depo ve Ürünleri görür. Finansal verileri **GÖRMEZ**. | `depo@demo.local` |
| **SatisTemsilcisi** | Plasiyer | Sipariş alır, Cari kart açar. Fatura kesme veya Tahsilat yetkisi kısıtlıdır. | `satis@demo.local` |

---

## 🔄 Optimistic Concurrency

Her entity `RowVersion` (byte[]) içerir. Güncelleme/silme işlemlerinde concurrency kontrolü yapılır.

### Akış
1. **GET** `/api/invoices/5` → `rowVersion: "AAAAAAAAB9E="` döner
2. **PUT** `/api/invoices/5` → Body'de `rowVersion` gönder
3. Başka biri aynı kaydı değiştirdiyse → **409 Conflict**

### Handler Pattern
```csharp
// 1. Fetch with tracking
var entity = await _db.Entities.FirstOrDefaultAsync(x => x.Id == id);

// 2. Set OriginalValue
var originalBytes = Convert.FromBase64String(req.RowVersion);
_db.Entry(entity).Property(nameof(Entity.RowVersion)).OriginalValue = originalBytes;

// 3. Update properties
entity.Name = req.Name;
entity.UpdatedAtUtc = DateTime.UtcNow;

// 4. Save with concurrency check
try {
    await _db.SaveChangesAsync();
} catch (DbUpdateConcurrencyException) {
    throw new ConcurrencyConflictException("Record was modified by another user.");
}
```

---

## 💰 Money & Decimal Policy

### Neden Decimal?
IEEE-754 double'da yuvarlama hataları var. Para hesaplamalarında `decimal` zorunlu.

### Kurallar
- **Veritabanı**: `decimal(18,2)` veya `decimal(18,3)` (stok için)
- **DTO**: String olarak (`"1500.00"`)
- **Parsing**: `Money.TryParse2()` veya `Money.TryParse3()`
- **Formatting**: `Money.S2()` veya `Money.S3()`
- **Yuvarlama**: `MidpointRounding.AwayFromZero`

### Örnek
```json
{
  "amount": "1500.00",
  "currency": "TRY",
  "vatAmount": "270.00",
  "grossAmount": "1770.00"
}
```

**Frontend**: Hesaplamalar backend'de yapılır, frontend sadece gösterir.

---

## 📋 Expense Workflow

```
Draft → Reviewed → Posted
  │         │         │
  └─ Edit   └─ Lock   └─ Invoice Created
```

### Adımlar
1. **Draft**: Masraf listesi oluştur, satırlar ekle
2. **Review**: Onay → artık düzenlenemez
3. **Post to Bill**: Satın alma faturasına çevir
   - CreatePayment=true → Otomatik ödeme kaydı

### Endpoint Örneği
```bash
POST /api/expense-lists/5/post-to-bill
{
  "expenseListId": 5,
  "supplierId": 10,
  "itemId": 3,
  "currency": "TRY",
  "createPayment": true,
  "paymentAccountId": 2
}
```

---

## 📊 Stock Management Workflow

### Initial Setup
1. **Warehouse oluştur**: `POST /api/warehouses`
2. **Item tanımla**: `POST /api/items`

### Stock Hareketleri
```bash
# Alış (stok girişi)
POST /api/stock-movements
{
  "branchId": 1,
  "warehouseId": 1,
  "itemId": 5,
  "type": "PurchaseIn",
  "quantity": "100.000",
  "transactionDateUtc": "2025-01-04T10:00:00Z"
}

# Satış (stok çıkışı)
POST /api/stock-movements
{
  "type": "SalesOut",
  "quantity": "10.000"
}
```

### Stok Sorgulama
```bash
GET /api/stocks?warehouseId=1&itemId=5
```

**Constraint**: Stok negatif olamaz (DB check constraint)

---

## 💰 Decimal & JSON Serialization

### Yaklaşım: JsonConverter ile Otomatik Formatlama

Tüm finansal değerler (tutar, miktar, fiyat) için **merkezi JSON converter** pattern'i kullanılmaktadır. Bu sayede:
- Handler'larda manuel `string` dönüşümü gerekmez
- Tutarlı format garantisi (ör: her zaman `"1250.50"`, asla `1250.5`)
- Tek noktadan kontrol (converter değişince tüm API etkilenir)

### JSON Converters

| Converter | Hassasiyet | Kullanım Alanı | Input/Output |
|-----------|------------|----------------|--------------|
| `AmountJsonConverter` | 2 hane | Tutar, Toplam, Bakiye, Fiyat | `"1250.50"` |
| `QuantityJsonConverter` | 3 hane | Miktar, Adet, Kilo | `"1.500"` |
| `UnitPriceJsonConverter` | 4 hane | Birim Fiyat (maliyet) | `"10.5045"` |
| `PercentJsonConverter` | 2 hane | İskonto, Vergi Oranı | `"18.00"` |

### DTO Örneği

```csharp
public record InvoiceLineDto(
    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Qty,                    // → "1.500"
    
    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,              // → "10.5000"
    
    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Total                   // → "15.75"
);
```

### Özellikler
- **Bi-directional:** Hem input (request) hem output (response) için çalışır
- **Flexible Input:** String (`"100.50"`) veya number (`100.5`) kabul eder
- **Consistent Output:** Her zaman string formatında döner
- **Auto-rounding:** `MidpointRounding.AwayFromZero` ile yuvarlar

### DecimalExtensions (Hesaplama için)

Handler'larda hesaplama yaparken:
```csharp
var lineNet = DecimalExtensions.RoundAmount(qty * unitPrice);  // 2 hane
var roundedQty = DecimalExtensions.RoundQuantity(qty);         // 3 hane
```

---

## 📦 Sipariş ve Fatura Fiyatlandırması

### KOBİ Kullanım Prensibi

> **"Stok kartını seçince fiyat gelsin, ama ben üzerine yazabileyim"**

Bu Türkiye'deki KOBİ'lerin en yaygın kullanım şeklidir.

### Akış

```
┌─────────────────────────────────────────────────────────┐
│  1. Kullanıcı stok kartı seçer                          │
│     └─► Frontend: GET /api/items/{id}                   │
│                                                         │
│  2. Fiyat otomatik doldurulur                           │
│     └─► Satış Siparişi: item.SalesPrice                 │
│     └─► Alış Siparişi: item.PurchasePrice               │
│                                                         │
│  3. Kullanıcı isterse fiyatı değiştirir                 │
│     └─► Müşteriye özel fiyat, kampanya, toplu indirim   │
│                                                         │
│  4. Backend kullanıcının gönderdiği fiyatı kabul eder   │
│     └─► POST/PUT request'teki UnitPrice kullanılır      │
└─────────────────────────────────────────────────────────┘
```

### Neden Bu Yaklaşım?

| Senaryo | Açıklama |
|---------|----------|
| **Müşteriye özel fiyat** | VIP müşteriye %10 indirimli fiyat |
| **Kampanya** | Yılbaşı indirimi |
| **Toplu alım** | 100+ adet alımda birim fiyat düşer |
| **Geçmiş kayıt** | Eski fatura/sipariş orijinal fiyatı korur |

### Sorumluluk Dağılımı

| Katman | Sorumluluk |
|--------|------------|
| **Frontend** | Item seçilince fiyatı API'den çekip UnitPrice alanına doldurur |
| **Backend** | Request'teki UnitPrice değerini doğrudan kullanır |
| **Validation** | UnitPrice > 0 kontrolü yapar |

---

## 🌐 API Standartları

### Pagination
```json
{
  "items": [...],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 20
}
```

### Sorting
```
?sort=createdAtUtc:desc
?sort=name:asc
```

### Date Format
**UTC ISO-8601**: `2025-01-04T10:00:00Z`

### Error Responses (ProblemDetails)
- **400** Validation Error
- **404** Not Found
- **409** Concurrency Conflict

---

## 🗄️ Database Schema

### Key Tables
| Table | Description | Key Columns |
|-------|-------------|-------------|
| `Contacts` | Müşteri/Tedarikçi/Personel | `Type`, `TaxNumber` |
| `Items` | Ürün/Hizmet | `Code`, `Name`, `UnitPrice` |
| `Invoices` | Faturalar | `Type`, `ContactId`, `TotalGross`, `Balance` |
| `InvoiceLines` | Fatura Kalemleri | `InvoiceId`, `ItemId`, `Qty`, `UnitPrice` |
| `Payments` | Tahsilat/Tediye | `Direction`, `AccountId`, `LinkedInvoiceId` |
| `ExpenseLists` | Masraf Listeleri | `Status`, `PostedInvoiceId` |
| `ExpenseLines` | Masraf Satırları | `ExpenseListId`, `Amount`, `VatRate` |
| `Warehouses` | Depolar | `BranchId`, `Code`, `IsDefault` |
| `Stocks` | Anlık Stok | `WarehouseId`, `ItemId`, `Quantity` |
| `StockMovements` | Stok Hareketleri | `Type`, `Quantity`, `TransactionDateUtc` |

### Indexes
```sql
-- Performance için önerilen indexler
CREATE INDEX IX_Invoices_DateUtc_ContactId ON Invoices(DateUtc, ContactId);
CREATE INDEX IX_Payments_DateUtc_AccountId ON Payments(DateUtc, AccountId);
CREATE INDEX IX_Stocks_WarehouseId_ItemId ON Stocks(WarehouseId, ItemId);
CREATE UNIQUE INDEX UX_Stocks_Branch_Warehouse_Item ON Stocks(BranchId, WarehouseId, ItemId) WHERE IsDeleted = 0;
```

---

## 🧪 Testing Scenarios

### 1. Invoice + Payment Flow
```bash
# 1. Create sales invoice
POST /api/invoices { type: "Sales", contactId: 5, lines: [...] }
# Response: { id: 100, totalGross: "1770.00", balance: "1770.00" }

# 2. Create payment
POST /api/payments { 
  linkedInvoiceId: 100, 
  amount: "1770.00", 
  direction: "In" 
}
# Response: Invoice balance = 0

# 3. Verify balance
GET /api/invoices/100
# Response: { balance: "0.00" }
```

### 2. Expense Post to Bill
```bash
# 1. Create expense list
POST /api/expense-lists { name: "Ocak Masrafları", lines: [...] }

# 2. Review
POST /api/expense-lists/1/review

# 3. Post to bill with payment
POST /api/expense-lists/1/post-to-bill {
  supplierId: 10,
  itemId: 3,
  currency: "TRY",
  createPayment: true,
  paymentAccountId: 2
}
# Response: { createdInvoiceId: 101, postedExpenseCount: 5 }
```

### 3. Stock Movement
```bash
# 1. Create warehouse
POST /api/warehouses { branchId: 1, code: "W01", name: "Ana Depo" }

# 2. Purchase (stock in)
POST /api/stock-movements {
  warehouseId: 1,
  itemId: 5,
  type: "PurchaseIn",
  quantity: "100.000"
}

# 3. Check stock
GET /api/stocks?warehouseId=1&itemId=5
# Response: { quantity: "100.000" }

# 4. Sales (stock out)
POST /api/stock-movements {
  warehouseId: 1,
  itemId: 5,
  type: "SalesOut",
  quantity: "10.000"
}

# 5. Verify
GET /api/stocks?warehouseId=1&itemId=5
# Response: { quantity: "90.000" }
```

### 4. Concurrency Test
```bash
# 1. Get record
GET /api/invoices/100
# Response: { rowVersion: "AAAAAAAAB9E=" }

# 2. Two users try to update
# User A:
PUT /api/invoices/100 { name: "Updated A", rowVersion: "AAAAAAAAB9E=" }
# Success: 200 OK

# User B (same rowVersion):
PUT /api/invoices/100 { name: "Updated B", rowVersion: "AAAAAAAAB9E=" }
# Fail: 409 Conflict
```

---

## 🚀 Running the Project

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or Express)

### Setup
```bash
# 1. Restore packages
dotnet restore

# 2. Update connection string (appsettings.json)
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AccountingDb;..."
}

# 3. Run migrations
dotnet ef database update --project Accounting.Infrastructure

# 4. Run API
dotnet run --project Accounting.Api
```

### Swagger
```
https://localhost:5001/swagger
```

---

## 📝 DTO Naming Convention

Projede tutarlı DTO isimlendirmesi kullanılmaktadır:

| Kullanım | Suffix | Örnek |
|----------|--------|-------|
| Tek kayıt (GetById) | `DetailDto` | `InvoiceDetailDto` |
| Liste item | `ListItemDto` | `InvoiceListItemDto` |
| Child/Nested | `Dto` | `InvoiceLineDto` |
| Command result | `Result` | `CreateInvoiceResult` |

### Örnek Kullanım

```csharp
// Controller
[HttpGet]
public Task<PagedResult<InvoiceListItemDto>> List(...)  // Liste

[HttpGet("{id}")]
public Task<InvoiceDetailDto> GetById(int id)           // Tek kayıt

[HttpPost]
public Task<CreateInvoiceResult> Create(...)            // Create result

[HttpPut("{id}")]
public Task<InvoiceDetailDto> Update(...)               // Update response
```

---

## 📁 Project Structure

```
Accounting.Api/
├── Controllers/
│   ├── ContactsController.cs
│   ├── InvoicesController.cs
│   ├── PaymentsController.cs
│   ├── ExpenseListsController.cs
│   ├── StocksController.cs
│   └── ...
└── Program.cs

Accounting.Application/
├── Contacts/
│   ├── Commands/ (Create, Update, Delete)
│   └── Queries/ (GetById, List)
├── Invoices/
├── Payments/
├── ExpenseLists/
├── Stocks/
├── Warehouses/
├── Cheques/
├── Users/
├── Roles/
└── Common/
    ├── Abstractions/ (IAppDbContext)
    ├── Behaviors/ (Validation, Transaction)
    ├── Errors/ (Exceptions)
    └── Utils/ (Money, PagedResult)

Accounting.Domain/
├── Entities/
│   ├── Contact.cs
│   ├── Invoice.cs
│   ├── Stock.cs
│   └── ...
├── Enums/
│   ├── InvoiceType.cs
│   └── StockMovementType.cs
└── Common/ (Interfaces)

Accounting.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/ (Entity configurations)
│   └── Seed/ (DataSeeder)
└── Interceptors/ (AuditSaveChangesInterceptor)
```

---

## 🎯 Next Steps (Future Features)

- [x] Invoice → Stock integration (otomatik stok hareketi)
- [x] Multi-branch stock transfer
- [x] Item Category support
- [x] Order Management (Quotes/Orders -> Invoice flow)
- [x] User authentication & authorization (JWT + Roles)
- [x] Cheque/Promissory Note Management
- [x] Multi-Currency Support (Payments/Invoices)
- [ ] Fixed Asset depreciation calculation
- [ ] Reporting module (balance sheet, P&L)
- [ ] Excel export support
- [ ] Audit log tracking (Basic Audit implemented, UI needed)
- [ ] Email notifications

---

## 📝 Notes

### Enums Namespace
Tüm enum'lar `Accounting.Domain.Enums` namespace'inde toplanmıştır:
- InvoiceType
- PaymentDirection
- ExpenseListStatus
- StockMovementType
- CashBankAccountType

### Entity Naming
- `ExpenseLine` (eski adı: Expense)
- `InvoiceLine` (fatura kalemi)
- Tüm liste entity'leri çoğul: `ExpenseLists`, `Invoices`, `Stocks`

---

**© 2026 Accounting & Inventory Management System**  
Clean Architecture + CQRS + DDD
