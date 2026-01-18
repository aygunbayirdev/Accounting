using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateExpensesAndFixedAssetsIntoItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_ExpenseDefinitions_ExpenseDefinitionId",
                table: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "ExpenseDefinitions");

            migrationBuilder.DropTable(
                name: "ExpenseLines");

            migrationBuilder.DropTable(
                name: "FixedAssets");

            migrationBuilder.DropTable(
                name: "ExpenseLists");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_ExpenseDefinitionId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ExpenseDefinitionId",
                table: "InvoiceLines");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseAccountCode",
                table: "Items",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesAccountCode",
                table: "Items",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsefulLifeYears",
                table: "Items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchaseAccountCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SalesAccountCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UsefulLifeYears",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Invoices");

            migrationBuilder.AddColumn<int>(
                name: "ExpenseDefinitionId",
                table: "InvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DefaultVatRate = table.Column<int>(type: "int", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseDefinitions", x => x.Id);
                    table.CheckConstraint("CK_ExpenseDef_VatRange", "[DefaultVatRate] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ExpenseDefinitions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PostedInvoiceId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseLists_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepreciationRatePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PurchaseDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsefulLifeYears = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssets", x => x.Id);
                    table.CheckConstraint("CK_FixedAssets_DepRate", "[DepreciationRatePercent] >= 0 AND [DepreciationRatePercent] <= 100");
                    table.CheckConstraint("CK_FixedAssets_UsefulLife", "[UsefulLifeYears] >= 1 AND [UsefulLifeYears] <= 100");
                    table.ForeignKey(
                        name: "FK_FixedAssets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseListId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PostedInvoiceId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VatRate = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseLines", x => x.Id);
                    table.CheckConstraint("CK_ExpenseLine_VatRate_Range", "[VatRate] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ExpenseLines_ExpenseLists_ExpenseListId",
                        column: x => x.ExpenseListId,
                        principalTable: "ExpenseLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_ExpenseDefinitionId",
                table: "InvoiceLines",
                column: "ExpenseDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDefinitions_BranchId_Code",
                table: "ExpenseDefinitions",
                columns: new[] { "BranchId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLines_DateUtc",
                table: "ExpenseLines",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLines_ExpenseListId",
                table: "ExpenseLines",
                column: "ExpenseListId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLines_SupplierId_DateUtc",
                table: "ExpenseLines",
                columns: new[] { "SupplierId", "DateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLists_BranchId",
                table: "ExpenseLists",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLists_CreatedAtUtc",
                table: "ExpenseLists",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLists_Status",
                table: "ExpenseLists",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_BranchId",
                table: "FixedAssets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_Code",
                table: "FixedAssets",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_ExpenseDefinitions_ExpenseDefinitionId",
                table: "InvoiceLines",
                column: "ExpenseDefinitionId",
                principalTable: "ExpenseDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
