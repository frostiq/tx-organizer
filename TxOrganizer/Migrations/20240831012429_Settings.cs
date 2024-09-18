using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TxOrganizer.Migrations
{
    /// <inheritdoc />
    public partial class Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CoinGeckoIds",
                keyColumn: "Symbol",
                keyValue: "btc");

            migrationBuilder.DeleteData(
                table: "CoinGeckoIds",
                keyColumn: "Symbol",
                keyValue: "eth");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "DATETIME");

            migrationBuilder.CreateTable(
                name: "Setting",
                columns: table => new
                {
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setting", x => new { x.Type, x.Key });
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
                name: "Setting");

            migrationBuilder.DeleteData(
                table: "CoinGeckoIds",
                keyColumn: "Symbol",
                keyValue: "BTC");

            migrationBuilder.DeleteData(
                table: "CoinGeckoIds",
                keyColumn: "Symbol",
                keyValue: "ETH");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Transactions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "Transactions",
                type: "DATETIME",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.InsertData(
                table: "CoinGeckoIds",
                columns: new[] { "Symbol", "CoinId" },
                values: new object[,]
                {
                    { "btc", "bitcoin" },
                    { "eth", "ethereum" }
                });
        }
    }
}
