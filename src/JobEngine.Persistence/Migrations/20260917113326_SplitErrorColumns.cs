using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobEngine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitErrorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "last_error",
                table: "jobs",
                newName: "last_error_message");

            migrationBuilder.AddColumn<string>(
                name: "last_error_detail",
                table: "jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_error_detail",
                table: "jobs");

            migrationBuilder.RenameColumn(
                name: "last_error_message",
                table: "jobs",
                newName: "last_error");
        }
    }
}
