using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditWorks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCategoryNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VehicleCategories_Name",
                table: "VehicleCategories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleCategories_Name",
                table: "VehicleCategories");
        }
    }
}
