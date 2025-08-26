using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class DebrisAndClosuresSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoadClosures_Status_UpdatedAt",
                table: "RoadClosures");

            migrationBuilder.DropIndex(
                name: "IX_DebrisRequests_ZoneId_Status_Priority",
                table: "DebrisRequests");

            migrationBuilder.CreateIndex(
                name: "IX_DebrisRequests_ZoneId",
                table: "DebrisRequests",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DebrisRequests_ZoneId",
                table: "DebrisRequests");

            migrationBuilder.CreateIndex(
                name: "IX_RoadClosures_Status_UpdatedAt",
                table: "RoadClosures",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DebrisRequests_ZoneId_Status_Priority",
                table: "DebrisRequests",
                columns: new[] { "ZoneId", "Status", "Priority" });
        }
    }
}
