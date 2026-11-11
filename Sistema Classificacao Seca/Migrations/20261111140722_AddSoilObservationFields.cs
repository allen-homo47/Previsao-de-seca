
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema_Classificacao_Seca.Migrations
{
    /// <inheritdoc />
    public partial class AddSoilObservationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BulkDensity",
                table: "SoilObservations",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SoilObservations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SoilObservations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Point",
                table: "SoilObservations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "S1_VV_dB",
                table: "SoilObservations",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SM_Percentile",
                table: "SoilObservations",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Silt",
                table: "SoilObservations",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BulkDensity",
                table: "SoilObservations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SoilObservations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SoilObservations");

            migrationBuilder.DropColumn(
                name: "Point",
                table: "SoilObservations");

            migrationBuilder.DropColumn(
                name: "S1_VV_dB",
                table: "SoilObservations");

            migrationBuilder.DropColumn(
                name: "SM_Percentile",
                table: "SoilObservations");

            migrationBuilder.DropColumn(
                name: "Silt",
                table: "SoilObservations");
        }
    }
}
