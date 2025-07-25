using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TxOrganizer.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddressLabels",
                columns: table => new
                {
                    Address = table.Column<string>(type: "TEXT", maxLength: 42, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressLabels", x => x.Address);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressLabels_Category",
                table: "AddressLabels",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AddressLabels_Label",
                table: "AddressLabels",
                column: "Label");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressLabels");
        }
    }
}
