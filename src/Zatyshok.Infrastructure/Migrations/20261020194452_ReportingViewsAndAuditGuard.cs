using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zatyshok.Infrastructure.Migrations
{
    /// <summary>
    /// Database objects that the EF model does not describe, written by hand as SQL (12.8.3, 12.8.4):
    /// the two views of the "reporting" schema and the trigger that keeps audit."AuditLogs"
    /// append-only. Grants for the application role are not here: creating roles needs a superuser,
    /// so they live in deploy/sql/roles-and-grants.sql.
    /// Note for later migrations: PostgreSQL refuses to change the type of a column that a view
    /// uses, so such a migration has to drop the view first and create it again.
    /// </summary>
    public partial class ReportingViewsAndAuditGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "reporting");

            // Listing 12.3: free rooms per (hotel, category) and night for 366 days from the hotel's
            // current date (NFR-20). Pairs come from Rates, so a category with all rooms out of order
            // stays in the result with rooms_total = 0; a negative rooms_free is a shortage (UC-16 A2).
            migrationBuilder.Sql("""
                CREATE VIEW reporting.v_category_availability_daily AS
                WITH inventory AS (
                    SELECT ra."HotelId", ra."RoomCategoryId",
                           count(r."Id") FILTER (WHERE r."IsActive" AND r."Status" <> 'OutOfOrder') AS rooms_total
                    FROM rooms."Rates" AS ra
                    LEFT JOIN rooms."Rooms" AS r
                           ON r."HotelId" = ra."HotelId" AND r."RoomCategoryId" = ra."RoomCategoryId"
                    GROUP BY ra."HotelId", ra."RoomCategoryId"
                ),
                demand AS (
                    SELECT b."HotelId", b."RoomCategoryId", n."Night", count(*) AS rooms_booked
                    FROM bookings."Bookings" AS b
                    JOIN bookings."BookingNights" AS n ON n."BookingId" = b."Id"
                    WHERE b."Status" IN ('Confirmed', 'CheckedIn')
                    GROUP BY b."HotelId", b."RoomCategoryId", n."Night"
                )
                SELECT i."HotelId"                                   AS hotel_id,
                       i."RoomCategoryId"                            AS room_category_id,
                       d.night,
                       i.rooms_total,
                       COALESCE(dm.rooms_booked, 0)                  AS rooms_booked,
                       i.rooms_total - COALESCE(dm.rooms_booked, 0)  AS rooms_free
                FROM inventory AS i
                JOIN rooms."Hotels" AS h ON h."Id" = i."HotelId" AND h."IsActive"
                CROSS JOIN LATERAL (
                    SELECT (now() AT TIME ZONE h."TimeZoneId")::date + g.k AS night
                    FROM generate_series(0, 365) AS g(k)
                ) AS d
                LEFT JOIN demand AS dm
                       ON dm."HotelId" = i."HotelId"
                      AND dm."RoomCategoryId" = i."RoomCategoryId"
                      AND dm."Night" = d.night;
                """);

            // Listing 12.4: occupied rooms and revenue per hotel and night (FR-35, FR-36). Nights
            // without occupied rooms are absent, so reports take the denominator from
            // rooms_in_inventory × number of days, not from the number of rows.
            migrationBuilder.Sql("""
                CREATE VIEW reporting.v_hotel_occupancy_daily AS
                WITH capacity AS (
                    SELECT r."HotelId", count(*) AS rooms_in_inventory
                    FROM rooms."Rooms" AS r
                    WHERE r."IsActive"
                    GROUP BY r."HotelId"
                ),
                occupied AS (
                    SELECT b."HotelId", n."Night", count(*) AS rooms_occupied
                    FROM bookings."Bookings" AS b
                    JOIN bookings."BookingNights" AS n ON n."BookingId" = b."Id"
                    WHERE b."Status" IN ('CheckedIn', 'Completed')
                    GROUP BY b."HotelId", n."Night"
                ),
                revenue AS (
                    SELECT b."HotelId", ii."ServiceDate" AS "Night",
                           sum(ii."Amount") FILTER (WHERE ii."Kind" = 'Accommodation') AS room_revenue,
                           sum(ii."Amount") FILTER (WHERE ii."Kind" = 'Service')       AS service_revenue
                    FROM billing."InvoiceItems" AS ii
                    JOIN billing."Invoices"  AS i ON i."Id" = ii."InvoiceId" AND i."Status" <> 'Voided'
                    JOIN bookings."Bookings" AS b ON b."Id" = i."BookingId"
                    GROUP BY b."HotelId", ii."ServiceDate"
                )
                SELECT o."HotelId"                     AS hotel_id,
                       o."Night"                       AS night,
                       c.rooms_in_inventory,
                       o.rooms_occupied,
                       round(100.0 * o.rooms_occupied / NULLIF(c.rooms_in_inventory, 0), 2) AS occupancy_pct,
                       COALESCE(rv.room_revenue, 0)    AS room_revenue,
                       COALESCE(rv.service_revenue, 0) AS service_revenue
                FROM occupied AS o
                JOIN capacity AS c ON c."HotelId" = o."HotelId"
                LEFT JOIN revenue AS rv ON rv."HotelId" = o."HotelId" AND rv."Night" = o."Night";
                """);

            // Listing 12.5: the audit log is append-only (NFR-10). UPDATE and TRUNCATE are always
            // rejected; DELETE only for records older than 3 years (cleanup by the Worker). The
            // exception cancels the whole statement. In the TRUNCATE (statement-level) trigger OLD
            // is NULL, so the function goes straight to RAISE.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.forbid_audit_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' AND OLD."OccurredAtUtc" < now() - interval '3 years' THEN
                        RETURN OLD;
                    END IF;
                    RAISE EXCEPTION 'Журнал аудиту незмінний (NFR-10): операцію % заборонено', TG_OP;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_AuditLogs_AppendOnly"
                    BEFORE UPDATE OR DELETE ON audit."AuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION audit.forbid_audit_change();
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_AuditLogs_NoTruncate"
                    BEFORE TRUNCATE ON audit."AuditLogs"
                    FOR EACH STATEMENT EXECUTE FUNCTION audit.forbid_audit_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "TR_AuditLogs_NoTruncate" ON audit."AuditLogs";""");
            migrationBuilder.Sql("""DROP TRIGGER "TR_AuditLogs_AppendOnly" ON audit."AuditLogs";""");
            migrationBuilder.Sql("DROP FUNCTION audit.forbid_audit_change();");

            migrationBuilder.Sql("DROP VIEW reporting.v_hotel_occupancy_daily;");
            migrationBuilder.Sql("DROP VIEW reporting.v_category_availability_daily;");

            // EF has no reverse operation for EnsureSchema, so the schema is dropped explicitly
            // (the same defect as in FullDomainModel).
            migrationBuilder.DropSchema(name: "reporting");
        }
    }
}
