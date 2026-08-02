using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAgentStateToJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Всё, что уже лежит в колонке, восстановлению не подлежит: jsonb пересортировал ключи,
            // и дискриминатор "$type" больше не первый — десериализация на таком состоянии падает.
            // Смена типа колонки этого не чинит, порядок ключей уже потерян. Чистим: история
            // переписки в messages цела, сессии просто соберутся из неё заново.
            migrationBuilder.Sql("UPDATE conversations SET agent_state = NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "agent_state",
                table: "conversations",
                type: "json",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "agent_state",
                table: "conversations",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "json",
                oldNullable: true);
        }
    }
}
