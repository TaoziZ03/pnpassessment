using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PnP.Scanning.Core.Storage.DatabaseMigration
{
    [DbContext(typeof(ScanContext))]
    [Migration("20260909213000_v1.13.1-classic-aspx-completeness")]
    public sealed class v1131classicaspxcompleteness : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HomePageKnown",
                table: "ClassicPages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LibraryHidden",
                table: "ClassicPages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WelcomePageStatus",
                table: "ClassicPages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(name: "AllAspxObserved", table: "ClassicWebSummaries", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ClassicExcluded", table: "ClassicWebSummaries", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ClassicSelected", table: "ClassicWebSummaries", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "HomePageOnly", table: "ClassicWebSummaries", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "HiddenAspxObserved", table: "ClassicWebSummaries", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "PageDiscoveryGapCodes", table: "ClassicWebSummaries", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PageDiscoveryOutputVersion", table: "ClassicWebSummaries", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PageDiscoveryState", table: "ClassicWebSummaries", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PageSelectionNotEvaluated", table: "ClassicWebSummaries", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "WelcomePageEvidence", table: "ClassicWebSummaries", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "WelcomePageStatus", table: "ClassicWebSummaries", type: "TEXT", nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassicPageDiscoveries",
                columns: table => new
                {
                    ScanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SiteUrl = table.Column<string>(type: "TEXT", nullable: false),
                    WebUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    OutputVersion = table.Column<string>(type: "TEXT", nullable: true),
                    PageName = table.Column<string>(type: "TEXT", nullable: true),
                    PageType = table.Column<string>(type: "TEXT", nullable: true),
                    ListUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ListTitle = table.Column<string>(type: "TEXT", nullable: true),
                    ListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    ObservationState = table.Column<string>(type: "TEXT", nullable: true),
                    SelectionState = table.Column<string>(type: "TEXT", nullable: true),
                    HomePage = table.Column<bool>(type: "INTEGER", nullable: true),
                    WelcomePageStatus = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassicPageDiscoveries", x => new { x.ScanId, x.SiteUrl, x.WebUrl, x.PageUrl });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassicPageDiscoveries_ScanId_SiteUrl_WebUrl_PageUrl",
                table: "ClassicPageDiscoveries",
                columns: new[] { "ScanId", "SiteUrl", "WebUrl", "PageUrl" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ClassicPageDiscoveries");
            migrationBuilder.DropColumn(name: "HomePageKnown", table: "ClassicPages");
            migrationBuilder.DropColumn(name: "LibraryHidden", table: "ClassicPages");
            migrationBuilder.DropColumn(name: "WelcomePageStatus", table: "ClassicPages");
            migrationBuilder.DropColumn(name: "AllAspxObserved", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "ClassicExcluded", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "ClassicSelected", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "HomePageOnly", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "HiddenAspxObserved", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "PageDiscoveryGapCodes", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "PageDiscoveryOutputVersion", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "PageDiscoveryState", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "PageSelectionNotEvaluated", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "WelcomePageEvidence", table: "ClassicWebSummaries");
            migrationBuilder.DropColumn(name: "WelcomePageStatus", table: "ClassicWebSummaries");
        }
    }
}
