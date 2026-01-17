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
  - Money values remain `string` (e.g., "1250.00") for precision control.
  ```csharp
  // ✅ CORRECT
  public record CreateInvoiceCommand(
      DateTime DateUtc,
      InvoiceType Type,
      string Amount  // Money stays string
  );
  
  // ❌ WRONG - Don't use string for dates/enums
  public record CreateInvoiceCommand(
      string DateUtc,  // BAD
      string Type      // BAD
  );
  ```

## 3. Domain Patterns
- **Money Value Object**:
  - **NEVER** use raw `decimal` formatting manually.
  - Use `Accounting.Application.Common.Utils.Money` static helper.
  - `Money.R2(val)` / `Money.R4(val)` for rounding.
  - `Money.S2(val)` / `Money.S4(val)` for string output.
  - **Rounding Policy**: `MidpointRounding.AwayFromZero` (Example: 2.5 -> 3, -2.5 -> -3).
- **Entities**:
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
- **Positive Values**: Financial values (Qty, Price, Total) in DB must ALWAYS be **POSITIVE**.
  - Direction (Refund/Return) is determined by `InvoiceType`, NOT by the sign of the value.
- **Stock Movement**: Linked to Invoices, but managed via Domain Events or Service orchestration (ensure consistency).

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
