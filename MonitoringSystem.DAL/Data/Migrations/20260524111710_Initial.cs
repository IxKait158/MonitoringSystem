using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringSystem.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anomalies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    MetricName = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedValue = table.Column<double>(type: "double precision", nullable: false),
                    AnomalyScore = table.Column<double>(type: "double precision", nullable: false),
                    IsAnomaly = table.Column<bool>(type: "boolean", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anomalies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    MetricName = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricPoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anomalies_ServiceName_InstanceId_DetectedAt",
                table: "Anomalies",
                columns: new[] { "ServiceName", "InstanceId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricPoints_ServiceName_InstanceId_MetricName_Timestamp",
                table: "MetricPoints",
                columns: new[] { "ServiceName", "InstanceId", "MetricName", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Anomalies");

            migrationBuilder.DropTable(
                name: "MetricPoints");
        }
    }
}
