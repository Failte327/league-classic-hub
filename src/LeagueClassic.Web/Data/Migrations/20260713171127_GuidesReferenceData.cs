using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeagueClassic.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuidesReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guides_HeroTag",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "HeroTag",
                table: "Guides");

            migrationBuilder.AddColumn<int>(
                name: "ChampionId",
                table: "Guides",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SkillOrder",
                table: "Guides",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpellOneId",
                table: "Guides",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpellTwoId",
                table: "Guides",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Champions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IconPath = table.Column<string>(type: "text", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Champions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IconPath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SummonerSpells",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IconPath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SummonerSpells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuideItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuideId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuideItems_Guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "Guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuideItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guides_ChampionId",
                table: "Guides",
                column: "ChampionId");

            migrationBuilder.CreateIndex(
                name: "IX_Guides_SpellOneId",
                table: "Guides",
                column: "SpellOneId");

            migrationBuilder.CreateIndex(
                name: "IX_Guides_SpellTwoId",
                table: "Guides",
                column: "SpellTwoId");

            migrationBuilder.CreateIndex(
                name: "IX_Champions_Slug",
                table: "Champions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuideItems_GuideId_Sort",
                table: "GuideItems",
                columns: new[] { "GuideId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_GuideItems_ItemId",
                table: "GuideItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Category",
                table: "Items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Slug",
                table: "Items",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SummonerSpells_Slug",
                table: "SummonerSpells",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Guides_Champions_ChampionId",
                table: "Guides",
                column: "ChampionId",
                principalTable: "Champions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Guides_SummonerSpells_SpellOneId",
                table: "Guides",
                column: "SpellOneId",
                principalTable: "SummonerSpells",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Guides_SummonerSpells_SpellTwoId",
                table: "Guides",
                column: "SpellTwoId",
                principalTable: "SummonerSpells",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guides_Champions_ChampionId",
                table: "Guides");

            migrationBuilder.DropForeignKey(
                name: "FK_Guides_SummonerSpells_SpellOneId",
                table: "Guides");

            migrationBuilder.DropForeignKey(
                name: "FK_Guides_SummonerSpells_SpellTwoId",
                table: "Guides");

            migrationBuilder.DropTable(
                name: "Champions");

            migrationBuilder.DropTable(
                name: "GuideItems");

            migrationBuilder.DropTable(
                name: "SummonerSpells");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Guides_ChampionId",
                table: "Guides");

            migrationBuilder.DropIndex(
                name: "IX_Guides_SpellOneId",
                table: "Guides");

            migrationBuilder.DropIndex(
                name: "IX_Guides_SpellTwoId",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "ChampionId",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "SkillOrder",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "SpellOneId",
                table: "Guides");

            migrationBuilder.DropColumn(
                name: "SpellTwoId",
                table: "Guides");

            migrationBuilder.AddColumn<string>(
                name: "HeroTag",
                table: "Guides",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guides_HeroTag",
                table: "Guides",
                column: "HeroTag");
        }
    }
}
