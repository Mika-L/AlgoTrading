using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoTrading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExplorationJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Explorations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Universe = table.Column<string>(type: "TEXT", nullable: false),
                    From = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    To = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Catalog = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Objective = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Trials = table.Column<long>(type: "INTEGER", nullable: false),
                    MeanSharpe = table.Column<double>(type: "REAL", nullable: false),
                    SquaredDeviations = table.Column<double>(type: "REAL", nullable: false),
                    Best = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BestDeflatedSharpe = table.Column<decimal>(type: "TEXT", nullable: true),
                    RanAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Explorations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Explorations_Universe",
                table: "Explorations",
                column: "Universe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Explorations");
        }
    }
}
