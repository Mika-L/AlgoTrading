using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoTrading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StrategyJson = table.Column<string>(type: "TEXT", nullable: false),
                    From = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    To = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    InitialCash = table.Column<decimal>(type: "TEXT", nullable: false),
                    FinalEquity = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalReturn = table.Column<decimal>(type: "TEXT", nullable: false),
                    AnnualisedReturn = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "TEXT", nullable: false),
                    Calmar = table.Column<decimal>(type: "TEXT", nullable: false),
                    Sharpe = table.Column<decimal>(type: "TEXT", nullable: false),
                    WinRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCosts = table.Column<decimal>(type: "TEXT", nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Universe = table.Column<string>(type: "TEXT", nullable: false),
                    RanAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyBars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstrumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Open = table.Column<decimal>(type: "TEXT", nullable: false),
                    High = table.Column<decimal>(type: "TEXT", nullable: false),
                    Low = table.Column<decimal>(type: "TEXT", nullable: false),
                    Close = table.Column<decimal>(type: "TEXT", nullable: false),
                    AdjustedClose = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyBars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyBars_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_Fingerprint",
                table: "BacktestRuns",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_RanAt",
                table: "BacktestRuns",
                column: "RanAt");

            migrationBuilder.CreateIndex(
                name: "IX_DailyBars_InstrumentId_Date",
                table: "DailyBars",
                columns: new[] { "InstrumentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Symbol",
                table: "Instruments",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "DailyBars");

            migrationBuilder.DropTable(
                name: "Instruments");
        }
    }
}
