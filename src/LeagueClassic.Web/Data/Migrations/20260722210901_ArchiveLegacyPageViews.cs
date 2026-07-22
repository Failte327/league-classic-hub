using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueClassic.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveLegacyPageViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A true rename, not a drop/recreate — this table holds the site's
            // early-days hit-counter data we're archiving, not discarding.
            migrationBuilder.RenameTable(
                name: "PageViews",
                newName: "PageViewsLegacy");

            migrationBuilder.Sql(
                "ALTER TABLE \"PageViewsLegacy\" RENAME CONSTRAINT \"PK_PageViews\" TO \"PK_PageViewsLegacy\";");

            migrationBuilder.RenameIndex(
                name: "IX_PageViews_OccurredAt",
                table: "PageViewsLegacy",
                newName: "IX_PageViewsLegacy_OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_PageViewsLegacy_OccurredAt",
                table: "PageViewsLegacy",
                newName: "IX_PageViews_OccurredAt");

            migrationBuilder.Sql(
                "ALTER TABLE \"PageViewsLegacy\" RENAME CONSTRAINT \"PK_PageViewsLegacy\" TO \"PK_PageViews\";");

            migrationBuilder.RenameTable(
                name: "PageViewsLegacy",
                newName: "PageViews");
        }
    }
}
