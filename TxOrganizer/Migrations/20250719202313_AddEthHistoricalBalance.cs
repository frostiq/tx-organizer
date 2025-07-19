using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TxOrganizer.Migrations
{
    /// <inheritdoc />
    public partial class AddEthHistoricalBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EthHistoricalBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 42, nullable: false),
                    BlockNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    BlockTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionHash = table.Column<string>(type: "TEXT", maxLength: 66, nullable: false),
                    BalanceEth = table.Column<decimal>(type: "decimal(28,18)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EthHistoricalBalances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EthHistoricalBalances_Address_BlockNumber",
                table: "EthHistoricalBalances",
                columns: new[] { "Address", "BlockNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EthHistoricalBalances");
        }
    }
}
