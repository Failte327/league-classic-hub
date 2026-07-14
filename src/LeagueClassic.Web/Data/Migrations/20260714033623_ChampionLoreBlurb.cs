using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueClassic.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChampionLoreBlurb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Blurb",
                table: "Champions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lore",
                table: "Champions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Champions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Blurb",
                table: "Champions");

            migrationBuilder.DropColumn(
                name: "Lore",
                table: "Champions");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Champions");
        }
    }
}
