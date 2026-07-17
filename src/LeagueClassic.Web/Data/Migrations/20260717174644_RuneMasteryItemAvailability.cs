using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueClassic.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuneMasteryItemAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DdragonId",
                table: "Runes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Runes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Masteries",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runes_DdragonId",
                table: "Runes",
                column: "DdragonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Runes_DdragonId",
                table: "Runes");

            migrationBuilder.DropColumn(
                name: "DdragonId",
                table: "Runes");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Runes");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Masteries");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Items");
        }
    }
}
