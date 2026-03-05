using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carrotware.CMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrotApiClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarrotApiClients",
                columns: table => new
                {
                    ApiClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(NEWSEQUENTIALID())"),
                    ClientId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScopeToSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrotApiClients", x => x.ApiClientId);
                    table.ForeignKey(
                        name: "FK_CarrotApiClients_ScopeToSiteId",
                        column: x => x.ScopeToSiteId,
                        principalTable: "carrot_Sites",
                        principalColumn: "SiteID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarrotApiClients_ClientId",
                table: "CarrotApiClients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarrotApiClients_ScopeToSiteId",
                table: "CarrotApiClients",
                column: "ScopeToSiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarrotApiClients");
        }
    }
}
