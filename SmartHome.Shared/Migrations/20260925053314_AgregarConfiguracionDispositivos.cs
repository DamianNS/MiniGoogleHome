using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHome.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionDispositivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 25, nullable: false),
                    Estado = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MiniDTOUsuario",
                columns: table => new
                {
                    MinisId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuariosId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiniDTOUsuario", x => new { x.MinisId, x.UsuariosId });
                    table.ForeignKey(
                        name: "FK_MiniDTOUsuario_Usuarios_UsuariosId",
                        column: x => x.UsuariosId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MiniDTOUsuario_devices_MinisId",
                        column: x => x.MinisId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MiniDTOUsuario_UsuariosId",
                table: "MiniDTOUsuario",
                column: "UsuariosId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MiniDTOUsuario");

            migrationBuilder.DropTable(
                name: "devices");
        }
    }
}
