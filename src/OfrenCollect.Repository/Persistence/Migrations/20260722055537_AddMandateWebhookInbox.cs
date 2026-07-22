using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfrenCollect.Repository.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMandateWebhookInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MandateReference",
                table: "InboxMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MandateReference",
                table: "InboxMessages");
        }
    }
}
