using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LeaderboardEntries",
                table: "LeaderboardEntries");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "LeaderboardEntries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaderboardEntries",
                table: "LeaderboardEntries",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LeaderboardEntries",
                table: "LeaderboardEntries");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "LeaderboardEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeaderboardEntries",
                table: "LeaderboardEntries",
                column: "Id");
        }
    }
}
