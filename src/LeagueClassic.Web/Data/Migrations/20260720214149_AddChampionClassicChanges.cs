using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueClassic.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChampionClassicChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassicChanges",
                table: "Champions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassicChanges",
                table: "Champions");
        }
    }
}
