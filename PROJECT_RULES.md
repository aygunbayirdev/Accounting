# Project Rules & Standards

This document defines the coding standards, architectural patterns, and best practices for the **Accounting** project. All contributions must adhere to these rules to maintain consistency and quality.

## 1. Architecture & Design
- **Structure**: Follow **Clean Architecture** principles.
  - `Domain`: Enterprise logic, Entities, Value Objects. No dependencies.
  - `Application`: Business logic, CQRS Handlers, Interfaces. Depends on `Domain`.
  - `Infrastructure`: Implementation of interfaces (Db, External Services). Depends on `Application`.
  - `Api`: Entry point, Controllers. Depends on `Application` and `Infrastructure`.
- **CQRS**: Use **MediatR** for all business operations.
  - **Commands**: Modify state. Return `Task<T>` or `Task`. Suffix: `Command`.
  - **Queries**: Read state. Return simple DTOs. Suffix: `Query`.
  - **Handlers**: Logic resides here. Suffix: `Handler`.

## 2. Coding Standards (C# 12)
- **File-scoped Namespaces**: Use `namespace Accounting.Application.Features;` (no indentation).
- **Primary Constructors**: Use primary constructors for dependency injection in classes (Handlers, Controllers).
  ```csharp
  // YES
  public class CreateInvoiceHandler(IAppDbContext db) : IRequestHandler<...> { ... }
  ```
- **Implicit Usings**: Enabled. Avoid cluttering files with common System imports.
- **DTOs**: Use `record` types for DTOs. Immutable by default.
- **DTO Type Rules**:
  - Use **native types** (`DateTime`, `DateTime?`, `enum`) in Command/Query DTOs.
  - .NET model binding handles JSON ↔ DateTime/Enum conversion automatically.
  - **DO NOT** use string for dates or enums in DTOs - no manual parsing needed.
  - Money values use `decimal` with `JsonConverter` attribute.
  ```csharp
  // ✅ CORRECT
  public record CreateInvoiceCommand(
      DateTime DateUtc,
      InvoiceType Type,
      [property: JsonConverter(typeof(AmountJsonConverter))]
      decimal Amount
  );
  
  // ❌ WRONG - Don't use string for dates/enums
  public record CreateInvoiceCommand(
      string DateUtc,  // BAD
      string Type      // BAD
  );
  ```

### DTO Naming Convention

| Kullanım | Suffix | Açıklama |
|----------|--------|----------|
| Tek kayıt (GetById, Create, Update response) | `DetailDto` | Tüm alanlar + RowVersion |
| Liste item (List response) | `ListItemDto` | Özet alanlar, RowVersion yok |
| Command/Query result | `Result` | İşlem sonucu |
| Nested/Child DTO | `Dto` | Alt kayıtlar (Line, Details) |

**Örnekler:**
```csharp
// ✅ DOĞRU
InvoiceDetailDto      // GetById, Update response
InvoiceListItemDto    // List response
InvoiceLineDto        // Child record
CreateInvoiceResult   // Command result

// ❌ YANLIŞ
InvoiceDto            // Belirsiz - DetailDto mı ListItemDto mu?
InvoiceListDto        // ListItemDto olmalı
```

**Klasör Yapısı:**
```
Accounting.Application/{Entity}/
├── Commands/
│   ├── Create/
│   │   ├── CreateInvoiceCommand.cs
│   │   └── CreateInvoiceHandler.cs
│   └── Update/
├── Queries/
│   ├── Dto/
│   │   └── InvoiceDtos.cs  // DetailDto + ListItemDto + LineDto
│   ├── GetById/
│   └── List/
```

## 3. Domain Patterns

### Decimal Handling & JSON Serialization

Projede **tüm finansal değerler** (tutar, miktar, fiyat) için tutarlı bir yaklaşım kullanılmaktadır:

#### Temel Prensipler
- **DTO'larda `decimal` tipi kullan**, string değil
- **`JsonConverter` attribute** ile otomatik formatlama
- **Handler'larda manuel dönüşüm YAPMA** - serialization katmanı halleder

#### JSON Converters (Accounting.Application.Common.JsonConverters/)

| Converter | Hassasiyet | Kullanım | Örnek |
|-----------|------------|----------|-------|
| `AmountJsonConverter` | 2 hane | Tutar, Toplam, Bakiye | `"1250.50"` |
| `QuantityJsonConverter` | 3 hane | Miktar, Adet | `"1.500"` |
| `UnitPriceJsonConverter` | 4 hane | Birim Fiyat | `"10.5045"` |
| `PercentJsonConverter` | 2 hane | İskonto, Vergi Oranı | `"18.00"` |

#### DTO Örneği
```csharp
public record InvoiceLineDto(
    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Qty,
    
    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,
    
    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Total
);
```

#### Handler'da Kullanım
```csharp
// ✅ DOĞRU - Direkt decimal ata, converter halleder
return new InvoiceLineDto(
    Qty: line.Qty,
    UnitPrice: line.UnitPrice,
    Total: line.Total
);

// ❌ YANLIŞ - Manuel string dönüşümü YAPMA
return new InvoiceLineDto(
    Qty: Money.S3(line.Qty),  // YAPMA!
    ...
);
```

#### DecimalExtensions (Hesaplama için)
Handler'larda hesaplama yaparken yuvarlama gerekiyorsa:
```csharp
var lineNet = DecimalExtensions.RoundAmount(qty * unitPrice);  // 2 hane
var roundedQty = DecimalExtensions.RoundQuantity(qty);         // 3 hane
```

### Legacy Money Helper (Deprecated)
`Money.S2()`, `Money.R4()` gibi metodlar **artık kullanılmıyor**. Yeni kod için `DecimalExtensions` ve `JsonConverter` pattern'i kullanın.

### Entities
  - Keep entities **Rich** where possible (methods for logic), but public setters are currently permitted for practical CRUD simplification in this project.
  - **Soft Delete**: Entities implementing `ISoftDelete` must set `IsDeleted = true` instead of physical deletion.
  - **Concurrency**: `RowVersion` property **MUST** be initialized to `Array.Empty<byte>()` in the entity definition to support InMemory testing and prevent nullability errors.
  - **Use Global Query Filters**: `AppDbContext` and EntityConfigurations apply global filters for `ISoftDeletable`.
    - **DO NOT** manually filter by `!x.IsDeleted` in Application layer queries (Handlers).
    - Use `.IgnoreQueryFilters()` explicitly if you need to access deleted records (e.g., for restore functionality or Audit).

## 4. Application Patterns
- **Database Access**:
  - Use `IAppDbContext` abstraction. Do not access `DbContext` direct methods not in the interface.
  - **AsNoTracking**: Use `.AsNoTracking()` for all Read/Query operations.
- **Exceptions**:
  - **NotFound**: throw `new Accounting.Application.Common.Exceptions.NotFoundException("EntityName", id)`. Does NOT throw `KeyNotFoundException`.
  - **Concurrency**: throw `new ConcurrencyConflictException(...)` when `RowVersion` mismatches.
  - **Validation**: handled by FluentValidation pipeline.
- **Pagination**:
  - Use `Accounting.Application.Common.Constants.PaginationConstants`.
  - Always normalize inputs:
    ```csharp
    var page = PaginationConstants.NormalizePage(request.Page);
    var size = PaginationConstants.NormalizePageSize(request.PageSize);
    ```
- **Concurrency Control**:
  - Use Optimistic Concurrency with `RowVersion` (byte[]).
  - Use the cross-platform retry pattern (not SQL locking hints).
  - In `Update` handlers, explicitly check `OriginalValue` of RowVersion.
- **Branch Filtering** (Multi-Branch Security):
  - **MANDATORY**: All `List` and `GetById` query handlers for entities implementing `IHasBranch` **MUST** use `ApplyBranchFilter()` extension.
  - **Pattern**:
    ```csharp
    var query = _db.Entities
        .ApplyBranchFilter(_currentUserService)  // 👈 MUST come before Includes
        .Include(...)
        .AsNoTracking()
        .Where(...);
    ```
  - **Why**: Ensures branch-level data isolation. Admin and HQ users see all branches; regular users see only their branch. Placing it before `Include` ensures the filter is applied to the root query and maintains `IIncludableQueryable` flexibility downstream.
  - **Exception**: User/Role management handlers (admin-only, no branch filtering needed).

## 5. API Rules
- **Response Format**: Methods return DTOs or `Unit`.
- **Status Codes**:
  - `200 OK`: Successful synchronous command/query.
  - `404 Not Found`: Entity missing (handled by middleware via `NotFoundException`).
  - `409 Conflict`: Concurrency or business rule violation.
  - `400 Bad Request`: Validation failure.

## 6. Specific Business Rules

### Positive Values
- Financial values (Qty, Price, Total) in DB must ALWAYS be **POSITIVE**.
- Direction (Refund/Return) is determined by `InvoiceType`, NOT by the sign of the value.

### Stock Movement
- Linked to Invoices, but managed via Domain Events or Service orchestration (ensure consistency).

### Order/Invoice Line Pricing (Sipariş ve Fatura Satır Fiyatlandırması)

**KOBİ Kullanım Senaryosu:**
> "Stok kartını seçince fiyat gelsin, ama ben üzerine yazabileyim"

**Uygulama:**
1. **Frontend Sorumluluğu:** Item seçildiğinde API'den Item detayı çekilir
2. **Fiyat Ataması:** 
   - Satın Alma (`Purchase`) → `Item.PurchasePrice`
   - Satış (`Sales`) → `Item.SalesPrice`
3. **Kullanıcı Override:** Kullanıcı UnitPrice alanını manuel değiştirebilir
4. **Backend:** Kullanıcının gönderdiği `UnitPrice` değerini kabul eder

**Neden Bu Yaklaşım?**
- Müşteriye/tedarikçiye özel fiyat verilebilir
- Kampanya/indirim uygulanabilir
- Toplu alımlarda farklı fiyat olabilir
- Eski fatura/sipariş fiyatlarını korur (Item fiyatı değişse bile)

```
┌─────────────────────────────────────────────────────────┐
│  Frontend: Item Seçimi                                  │
│  ┌─────────────────┐                                    │
│  │ Stok Kartı: X   │ ──► GET /api/items/5               │
│  └─────────────────┘     Response: { salesPrice: 100 }  │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────┐                                    │
│  │ UnitPrice: 100  │ ◄── Otomatik doldur                │
│  └─────────────────┘                                    │
│           │                                             │
│           ▼ (Kullanıcı değiştirebilir)                  │
│  ┌─────────────────┐                                    │
│  │ UnitPrice: 95   │ ──► POST /api/orders               │
│  └─────────────────┘     { unitPrice: 95 }              │
└─────────────────────────────────────────────────────────┘
```

## 8. Authorization Policy 🛡️
- **Mechanism**: Dynamic Policy Authorization based on Permissions.
- **Rule**: All Controllers/Endpoints (except Auth/Public) **MUST** use `[Authorize(Policy = Permissions.Module.Action)]`.
- **Naming Convention**: `Permissions.Domain.Action` (e.g., `Permissions.Invoice.Create`).
- **Implementation**: Policies are dynamically registered in `DependencyInjection.cs` from `Permissions.GetAll()`.
- **Do NOT** use Role-based auth (`Roles="Admin"`) directly in controllers. Use Permissions to abstract roles.

## 7. Migration & Database
- **Schema**: Use `SnakeCase` naming for tables/columns (or preserve existing convention if Pascal).
- **UTC**: All `DateTime` fields must be UTC (`DateTime.UtcNow`). suffix `AtUtc` (e.g., `CreatedAtUtc`).

## 8. Project Scope & Vision
- **Core Domain**: Pre-Accounting (Ön Muhasebe) and Stock Management.
- **Reference Model**: Features and UX should take inspiration from **"Mikro Paraşüt"** SaaS application.
- **Goal**: Provide a tailored, efficient backend that replaces Excel for SMEs (KOBİ), without over-engineering enterprise ERP features unless requested.

## 9. Testing Strategy
- **Mandatory Unit Tests**: Every new feature (Command, Handler, Logic) **MUST** have corresponding unit tests.
- **InMemory Provider**: Tests use `Microsoft.EntityFrameworkCore.InMemory`.
  - **Limitations**: Does not support transactions (ignore `TransactionIgnoredWarning`). Enforces strict nullability checks.
  - **Seeding**: All `required` or non-nullable properties (e.g., `Code`, `Currency`, `RowVersion`) **MUST** be populated in test seeds.
- **Consolidation**: Group related tests (e.g., `AccountingTests.cs` for general flows) or separate by module (`ChequeTests.cs`) if complex.
- **Scope**: Verify Happy Path, Edge Cases, and Business Rule Exceptions.

## 10. Development Workflow
1.  **Entity/Domain**: Define entities, properties, and relationships.
2.  **Contracts**: Create/Update Commands, Queries, and DTOs.
3.  **Application Logic**: Implement Handlers and Validators.
4.  **Database**: Create Migrations (if Entity changed) and update `DataSeeder`.
5.  **TESTING**: Write/Update Unit Tests to verify the change. **(Do not skip)**.
6.  **Refactor**: Cleanup and optimize based on test results.
