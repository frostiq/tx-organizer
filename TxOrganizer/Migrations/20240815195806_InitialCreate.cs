using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TxOrganizer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoinGeckoIds",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    CoinId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinGeckoIds", x => x.Symbol);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    BuyAmount = table.Column<double>(type: "REAL", nullable: false),
                    BuyCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    SellAmount = table.Column<double>(type: "REAL", nullable: false),
                    SellCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    Fee = table.Column<double>(type: "REAL", nullable: false),
                    FeeCurrency = table.Column<string>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    USDEquivalent = table.Column<double>(type: "REAL", nullable: false),
                    TxHash = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CoinGeckoIds",
                columns: new[] { "Symbol", "CoinId" },
                values: new object[,]
                {
                    { "BTC", "bitcoin" },
                    { "ETH", "ethereum" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoinGeckoIds");

            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
