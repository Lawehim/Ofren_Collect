using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfrenCollect.Repository.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMandates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mandates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MandateReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MonnifyMandateCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mandates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mandates_MandateReference",
                table: "Mandates",
                column: "MandateReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mandates_SubscriptionId",
                table: "Mandates",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Mandates_TenantId",
                table: "Mandates",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mandates");
        }
    }
}
