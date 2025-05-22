using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExactlyOnce.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "exactly_once");

            migrationBuilder.CreateTable(
                name: "processed_inbox_messages",
                schema: "exactly_once",
                columns: table => new
                {
                    idempotence_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_inbox_messages", x => x.idempotence_key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_inbox_messages",
                schema: "exactly_once");
        }
    }
}
