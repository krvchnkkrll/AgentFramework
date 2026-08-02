using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "agent_id",
                table: "conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    icon = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    instructions = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    temperature = table.Column<float>(type: "real", nullable: false),
                    top_p = table.Column<float>(type: "real", nullable: false),
                    top_k = table.Column<int>(type: "integer", nullable: false),
                    max_output_tokens = table.Column<int>(type: "integer", nullable: false),
                    frequency_penalty = table.Column<float>(type: "real", nullable: false),
                    presence_penalty = table.Column<float>(type: "real", nullable: false),
                    reasoning_effort_enum = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    skills = table.Column<string>(type: "json", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agents", x => x.id);
                    table.ForeignKey(
                        name: "fk_agents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_agent_id",
                table: "conversations",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agents_user_id",
                table: "agents",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_conversations_agents_agent_id",
                table: "conversations",
                column: "agent_id",
                principalTable: "agents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_conversations_agents_agent_id",
                table: "conversations");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropIndex(
                name: "ix_conversations_agent_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "agent_id",
                table: "conversations");
        }
    }
}
