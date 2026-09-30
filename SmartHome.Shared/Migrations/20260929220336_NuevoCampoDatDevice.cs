using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHome.Shared.Migrations
{
    /// <inheritdoc />
    public partial class NuevoCampoDatDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "devices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Data",
                table: "devices");
        }
    }
}
