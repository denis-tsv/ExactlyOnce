using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

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
                name: "inbox_message_offsets",
                schema: "exactly_once",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    last_processed_offset = table.Column<long>(type: "bigint", nullable: false),
                    available_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_message_offsets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "exactly_once",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    offset = table.Column<long>(type: "bigint", nullable: false),
                    idempotence_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    headers = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

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

            migrationBuilder.InsertData(
                schema: "exactly_once",
                table: "inbox_message_offsets",
                columns: new[] { "id", "last_processed_offset", "partition", "topic" },
                values: new object[,]
                {
                    { 1, 0L, 0, "topic-1" },
                    { 2, 0L, 0, "topic-2" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_message_offsets_topic_partition",
                schema: "exactly_once",
                table: "inbox_message_offsets",
                columns: new[] { "topic", "partition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_topic_partition_offset",
                schema: "exactly_once",
                table: "inbox_messages",
                columns: new[] { "topic", "partition", "offset" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_message_offsets",
                schema: "exactly_once");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "exactly_once");

            migrationBuilder.DropTable(
                name: "processed_inbox_messages",
                schema: "exactly_once");
        }
    }
}
