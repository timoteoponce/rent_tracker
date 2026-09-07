using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAuditAndCollapseDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_LeaseId",
                table: "Payments");

            // Create the audit table first so the backfill below can populate it.
            migrationBuilder.CreateTable(
                name: "PaymentAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ForPeriod = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PaymentDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAudits_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAudits_PaymentId",
                table: "PaymentAudits",
                column: "PaymentId");

            // --- Backfill: collapse the old "new row per edit" history into PaymentAudits ---
            //
            // Before this change, editing a payment inserted a new row pointing at the
            // previous one via PreviousPaymentId, leaving several rows per lease + period.
            // Walk each chain from its surviving head back through its ancestors, snapshot
            // every ancestor into PaymentAudits (attributed to the head), then delete them.
            migrationBuilder.Sql(@"
                INSERT INTO ""PaymentAudits"" (""Id"", ""PaymentId"", ""Amount"", ""Currency"", ""Status"", ""ForPeriod"", ""PaymentDate"", ""Notes"", ""RecordedAt"", ""EditedByUserId"")
                WITH RECURSIVE chain(""headId"", ""ancestorId"", ""prevId"") AS (
                    SELECT p.""Id"", p.""Id"", p.""PreviousPaymentId""
                    FROM ""Payments"" p
                    WHERE NOT EXISTS (SELECT 1 FROM ""Payments"" s WHERE s.""PreviousPaymentId"" = p.""Id"")
                    UNION ALL
                    SELECT c.""headId"", a.""Id"", a.""PreviousPaymentId""
                    FROM chain c
                    JOIN ""Payments"" a ON a.""Id"" = c.""prevId""
                )
                SELECT
                    hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)),
                    chain.""headId"",
                    old.""Amount"", old.""Currency"", old.""Status"", old.""ForPeriod"", old.""PaymentDate"", old.""Notes"",
                    head.""CreatedAt"",
                    NULL
                FROM chain
                JOIN ""Payments"" old ON old.""Id"" = chain.""ancestorId""
                JOIN ""Payments"" head ON head.""Id"" = chain.""headId""
                WHERE chain.""ancestorId"" <> chain.""headId"";

                DELETE FROM ""Payments""
                WHERE ""Id"" IN (
                    WITH RECURSIVE chain(""headId"", ""ancestorId"", ""prevId"") AS (
                        SELECT p.""Id"", p.""Id"", p.""PreviousPaymentId""
                        FROM ""Payments"" p
                        WHERE NOT EXISTS (SELECT 1 FROM ""Payments"" s WHERE s.""PreviousPaymentId"" = p.""Id"")
                        UNION ALL
                        SELECT c.""headId"", a.""Id"", a.""PreviousPaymentId""
                        FROM chain c
                        JOIN ""Payments"" a ON a.""Id"" = c.""prevId""
                    )
                    SELECT ""ancestorId"" FROM chain WHERE ""ancestorId"" <> ""headId""
                );

                -- Drop auto-generated Pending placeholders where a real payment already
                -- exists for the same lease + period.
                DELETE FROM ""Payments""
                WHERE ""IsAutoGenerated"" = 1
                  AND ""Status"" = 'Pending'
                  AND EXISTS (
                      SELECT 1 FROM ""Payments"" other
                      WHERE other.""Id"" <> ""Payments"".""Id""
                        AND other.""LeaseId"" = ""Payments"".""LeaseId""
                        AND strftime('%Y-%m', other.""ForPeriod"") = strftime('%Y-%m', ""Payments"".""ForPeriod"")
                  );
            ");

            migrationBuilder.DropColumn(
                name: "PreviousPaymentId",
                table: "Payments");

            // Backstop the auto-generator against a race with a manual create.
            migrationBuilder.CreateIndex(
                name: "IX_Payments_LeaseId_ForPeriod",
                table: "Payments",
                columns: new[] { "LeaseId", "ForPeriod" },
                unique: true,
                filter: "[IsAutoGenerated] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The collapsed history rows are not restored (they live in PaymentAudits).
            migrationBuilder.DropTable(
                name: "PaymentAudits");

            migrationBuilder.DropIndex(
                name: "IX_Payments_LeaseId_ForPeriod",
                table: "Payments");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousPaymentId",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_LeaseId",
                table: "Payments",
                column: "LeaseId");
        }
    }
}
