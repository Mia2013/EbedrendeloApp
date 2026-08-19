using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ALaCarteItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    PriceHuf = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALaCarteItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyMenus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMenus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderingPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ALaCarteDailyOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ALaCarteItemId = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    OrderedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALaCarteDailyOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ALaCarteDailyOffers_ALaCarteItems_ALaCarteItemId",
                        column: x => x.ALaCarteItemId,
                        principalTable: "ALaCarteItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyMenuId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuVariants_DailyMenus_DailyMenuId",
                        column: x => x.DailyMenuId,
                        principalTable: "DailyMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KeresztNev = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VezetekNev = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Rf = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SzervKod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ALaCarteOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderingPeriodId = table.Column<int>(type: "int", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlacedByUserId = table.Column<int>(type: "int", nullable: false),
                    TotalHuf = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALaCarteOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ALaCarteOrders_OrderingPeriods_OrderingPeriodId",
                        column: x => x.OrderingPeriodId,
                        principalTable: "OrderingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ALaCarteOrders_Users_PlacedByUserId",
                        column: x => x.PlacedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ALaCarteOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuPortionHuf = table.Column<int>(type: "int", nullable: false),
                    ChangeDeadlineWorkingDays = table.Column<int>(type: "int", nullable: false),
                    ChangeDeadlineLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ALaCarteOrderDeadlineLocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExcludedDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcludedDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExcludedDays_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitchenClosures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: false),
                    TotalPortions = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenClosures_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OrderingPeriodId = table.Column<int>(type: "int", nullable: false),
                    MenuGrossHuf = table.Column<int>(type: "int", nullable: false),
                    ALaCarteGrossHuf = table.Column<int>(type: "int", nullable: false),
                    GrossHuf = table.Column<int>(type: "int", nullable: false),
                    CreditAppliedHuf = table.Column<int>(type: "int", nullable: false),
                    MenuPayableHuf = table.Column<int>(type: "int", nullable: false),
                    ALaCartePayableHuf = table.Column<int>(type: "int", nullable: false),
                    PayableHuf = table.Column<int>(type: "int", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MarkedPaidByUserId = table.Column<int>(type: "int", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodInvoices_OrderingPeriods_OrderingPeriodId",
                        column: x => x.OrderingPeriodId,
                        principalTable: "OrderingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodInvoices_Users_MarkedPaidByUserId",
                        column: x => x.MarkedPaidByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodInvoices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ALaCarteOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ALaCarteOrderId = table.Column<int>(type: "int", nullable: false),
                    ALaCarteDailyOfferId = table.Column<int>(type: "int", nullable: false),
                    ItemNameSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CategorySnapshot = table.Column<int>(type: "int", nullable: false),
                    UnitPriceHuf = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALaCarteOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ALaCarteOrderLines_ALaCarteDailyOffers_ALaCarteDailyOfferId",
                        column: x => x.ALaCarteDailyOfferId,
                        principalTable: "ALaCarteDailyOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ALaCarteOrderLines_ALaCarteOrders_ALaCarteOrderId",
                        column: x => x.ALaCarteOrderId,
                        principalTable: "ALaCarteOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderingPeriodId = table.Column<int>(type: "int", nullable: false),
                    MenuVariantId = table.Column<int>(type: "int", nullable: false),
                    PriceHuf = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlacedByUserId = table.Column<int>(type: "int", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CancellationReason = table.Column<int>(type: "int", nullable: true),
                    CancelledByExcludedDayId = table.Column<int>(type: "int", nullable: true),
                    ReassignedFromVariantCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ReassignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuOrders_ExcludedDays_CancelledByExcludedDayId",
                        column: x => x.CancelledByExcludedDayId,
                        principalTable: "ExcludedDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuOrders_MenuVariants_MenuVariantId",
                        column: x => x.MenuVariantId,
                        principalTable: "MenuVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuOrders_OrderingPeriods_OrderingPeriodId",
                        column: x => x.OrderingPeriodId,
                        principalTable: "OrderingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuOrders_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuOrders_Users_PlacedByUserId",
                        column: x => x.PlacedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitchenClosureLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KitchenClosureId = table.Column<int>(type: "int", nullable: false),
                    VariantCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    VariantNameSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenClosureLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenClosureLines_KitchenClosures_KitchenClosureId",
                        column: x => x.KitchenClosureId,
                        principalTable: "KitchenClosures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AmountHuf = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceMenuOrderId = table.Column<int>(type: "int", nullable: true),
                    RemainingHuf = table.Column<int>(type: "int", nullable: false),
                    ConsumesCreditEntryId = table.Column<int>(type: "int", nullable: true),
                    PeriodInvoiceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditEntries_CreditEntries_ConsumesCreditEntryId",
                        column: x => x.ConsumesCreditEntryId,
                        principalTable: "CreditEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditEntries_MenuOrders_SourceMenuOrderId",
                        column: x => x.SourceMenuOrderId,
                        principalTable: "MenuOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditEntries_PeriodInvoices_PeriodInvoiceId",
                        column: x => x.PeriodInvoiceId,
                        principalTable: "PeriodInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RelatedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RelatedMenuOrderId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_MenuOrders_RelatedMenuOrderId",
                        column: x => x.RelatedMenuOrderId,
                        principalTable: "MenuOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteDailyOffers_ALaCarteItemId",
                table: "ALaCarteDailyOffers",
                column: "ALaCarteItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteDailyOffers_Date_ALaCarteItemId",
                table: "ALaCarteDailyOffers",
                columns: new[] { "Date", "ALaCarteItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteOrderLines_ALaCarteDailyOfferId",
                table: "ALaCarteOrderLines",
                column: "ALaCarteDailyOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteOrderLines_ALaCarteOrderId_ALaCarteDailyOfferId",
                table: "ALaCarteOrderLines",
                columns: new[] { "ALaCarteOrderId", "ALaCarteDailyOfferId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteOrders_OrderingPeriodId",
                table: "ALaCarteOrders",
                column: "OrderingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteOrders_PlacedByUserId",
                table: "ALaCarteOrders",
                column: "PlacedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteOrders_UserId_Date",
                table: "ALaCarteOrders",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_UpdatedByUserId",
                table: "AppSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditEntries_ConsumesCreditEntryId",
                table: "CreditEntries",
                column: "ConsumesCreditEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditEntries_CreatedByUserId",
                table: "CreditEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditEntries_PeriodInvoiceId",
                table: "CreditEntries",
                column: "PeriodInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditEntries_SourceMenuOrderId",
                table: "CreditEntries",
                column: "SourceMenuOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditEntries_UserId",
                table: "CreditEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyMenus_Date",
                table: "DailyMenus",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExcludedDays_CreatedByUserId",
                table: "ExcludedDays",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcludedDays_Date",
                table: "ExcludedDays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosureLines_KitchenClosureId",
                table: "KitchenClosureLines",
                column: "KitchenClosureId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosures_ClosedByUserId",
                table: "KitchenClosures",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosures_Date",
                table: "KitchenClosures",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_CancelledByExcludedDayId",
                table: "MenuOrders",
                column: "CancelledByExcludedDayId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_CancelledByUserId",
                table: "MenuOrders",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_Date_Status",
                table: "MenuOrders",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_MenuVariantId",
                table: "MenuOrders",
                column: "MenuVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_OrderingPeriodId_UserId",
                table: "MenuOrders",
                columns: new[] { "OrderingPeriodId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_PlacedByUserId",
                table: "MenuOrders",
                column: "PlacedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_UserId_Date",
                table: "MenuOrders",
                columns: new[] { "UserId", "Date" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MenuVariants_DailyMenuId_Code",
                table: "MenuVariants",
                columns: new[] { "DailyMenuId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPeriods_EndDate",
                table: "OrderingPeriods",
                column: "EndDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPeriods_StartDate",
                table: "OrderingPeriods",
                column: "StartDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderingPeriods_StartDate_EndDate",
                table: "OrderingPeriods",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodInvoices_MarkedPaidByUserId",
                table: "PeriodInvoices",
                column: "MarkedPaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodInvoices_OrderingPeriodId",
                table: "PeriodInvoices",
                column: "OrderingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodInvoices_UserId_OrderingPeriodId",
                table: "PeriodInvoices",
                columns: new[] { "UserId", "OrderingPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_RelatedMenuOrderId",
                table: "UserNotifications",
                column: "RelatedMenuOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_ReadAtUtc_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "ReadAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserId",
                table: "Users",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ALaCarteOrderLines");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "CreditEntries");

            migrationBuilder.DropTable(
                name: "KitchenClosureLines");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "ALaCarteDailyOffers");

            migrationBuilder.DropTable(
                name: "ALaCarteOrders");

            migrationBuilder.DropTable(
                name: "PeriodInvoices");

            migrationBuilder.DropTable(
                name: "KitchenClosures");

            migrationBuilder.DropTable(
                name: "MenuOrders");

            migrationBuilder.DropTable(
                name: "ALaCarteItems");

            migrationBuilder.DropTable(
                name: "ExcludedDays");

            migrationBuilder.DropTable(
                name: "MenuVariants");

            migrationBuilder.DropTable(
                name: "OrderingPeriods");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "DailyMenus");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
