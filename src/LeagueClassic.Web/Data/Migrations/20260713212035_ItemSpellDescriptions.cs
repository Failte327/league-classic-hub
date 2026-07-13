using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueClassic.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItemSpellDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SummonerSpells",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "SummonerSpells");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Items");
        }
    }
}
