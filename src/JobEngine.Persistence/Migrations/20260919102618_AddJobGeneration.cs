using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobEngine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_job_executions_job_id_attempt",
                table: "job_executions");

            migrationBuilder.AddColumn<int>(
                name: "generation",
                table: "jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "generation",
                table: "job_executions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ux_job_executions_job_id_generation_attempt",
                table: "job_executions",
                columns: new[] { "job_id", "generation", "attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_job_executions_job_id_generation_attempt",
                table: "job_executions");

            migrationBuilder.DropColumn(
                name: "generation",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "generation",
                table: "job_executions");

            migrationBuilder.CreateIndex(
                name: "ux_job_executions_job_id_attempt",
                table: "job_executions",
                columns: new[] { "job_id", "attempt" },
                unique: true);
        }
    }
}
