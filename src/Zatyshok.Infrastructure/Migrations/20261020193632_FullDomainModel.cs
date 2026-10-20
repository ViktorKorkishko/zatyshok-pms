using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Zatyshok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FullDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "bookings");

            migrationBuilder.EnsureSchema(
                name: "guests");

            migrationBuilder.EnsureSchema(
                name: "rooms");

            migrationBuilder.EnsureSchema(
                name: "housekeeping");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.RenameTable(
                name: "SeasonRates",
                newName: "SeasonRates",
                newSchema: "rooms");

            migrationBuilder.RenameTable(
                name: "Rooms",
                newName: "Rooms",
                newSchema: "rooms");

            migrationBuilder.RenameTable(
                name: "RoomCategories",
                newName: "RoomCategories",
                newSchema: "rooms");

            migrationBuilder.RenameTable(
                name: "Rates",
                newName: "Rates",
                newSchema: "rooms");

            migrationBuilder.RenameTable(
                name: "Hotels",
                newName: "Hotels",
                newSchema: "rooms");

            migrationBuilder.CreateSequence(
                name: "BookingNumbers",
                schema: "bookings",
                startValue: 100001L);

            migrationBuilder.CreateSequence(
                name: "InvoiceNumbers",
                schema: "billing",
                startValue: 100001L);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "rooms",
                table: "Rooms",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<int>(
                name: "CancellationDeadlineDaysBefore",
                schema: "rooms",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CancellationDeadlineTime",
                schema: "rooms",
                table: "Hotels",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckInTime",
                schema: "rooms",
                table: "Hotels",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(14, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckOutTime",
                schema: "rooms",
                table: "Hotels",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(12, 0, 0));

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_Action", "\"Action\" IN ('Create', 'Update', 'Delete', 'StatusChange', 'Login', 'LoginFailed', 'Lockout', 'PasswordChange')");
                });

            migrationBuilder.CreateTable(
                name: "CorporateClients",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "uk-UA-x-icu"),
                    Edrpou = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContractStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentDeferralDays = table.Column<int>(type: "integer", nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateClients", x => x.Id);
                    table.CheckConstraint("CK_CorporateClients_Contract", "\"ContractStartDate\" IS NULL OR \"ContractEndDate\" IS NULL OR \"ContractEndDate\" >= \"ContractStartDate\"");
                    table.CheckConstraint("CK_CorporateClients_Edrpou", "\"Edrpou\" ~ '^[0-9]{8}$'");
                    table.CheckConstraint("CK_CorporateClients_PaymentDeferralDays", "\"PaymentDeferralDays\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_AttemptCount", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_OutboxMessages_SentAt", "(\"Status\" = 'Sent') = (\"SentAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_OutboxMessages_Status", "\"Status\" IN ('Pending', 'Sent', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsStaff = table.Column<bool>(type: "boolean", nullable: false),
                    IsHotelBound = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.CheckConstraint("CK_Roles_HotelBoundIsStaff", "NOT \"IsHotelBound\" OR \"IsStaff\"");
                });

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.CheckConstraint("CK_Services_Price", "\"Price\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "uk-UA-x-icu"),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_EmailLowerCase", "\"Email\" = lower(\"Email\")");
                    table.CheckConstraint("CK_Users_FailedLoginCount", "\"FailedLoginCount\" >= 0");
                    table.CheckConstraint("CK_Users_Status", "\"Status\" IN ('PendingConfirmation', 'Active', 'Blocked')");
                    table.ForeignKey(
                        name: "FK_Users_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalSchema: "rooms",
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTokens", x => x.Id);
                    table.CheckConstraint("CK_AccountTokens_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_AccountTokens_Purpose", "\"Purpose\" IN ('EmailConfirmation', 'PasswordReset')");
                    table.ForeignKey(
                        name: "FK_AccountTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "uk-UA-x-icu"),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "uk-UA-x-icu"),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "uk-UA-x-icu"),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Citizenship = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Preferences = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StaffNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PersonalDataConsentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnonymizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                    table.CheckConstraint("CK_Guests_Document", "(\"DocumentType\" IS NULL) = (\"DocumentNumber\" IS NULL)");
                    table.CheckConstraint("CK_Guests_DocumentType", "\"DocumentType\" IN ('Passport', 'IdCard', 'ForeignPassport', 'Other')");
                    table.ForeignKey(
                        name: "FK_Guests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_Expiry", "\"ExpiresAtUtc\" <= \"SessionExpiresAtUtc\"");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('bookings.\"BookingNumbers\"')"),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorporateClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GuestsCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Wishes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FreeCancellationUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_CheckedInAt", "(\"Status\" IN ('CheckedIn', 'Completed')) = (\"CheckedInAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Bookings_CheckedOutAt", "(\"Status\" = 'Completed') = (\"CheckedOutAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Bookings_Dates", "\"CheckOutDate\" > \"CheckInDate\"");
                    table.CheckConstraint("CK_Bookings_GuestsCount", "\"GuestsCount\" >= 1");
                    table.CheckConstraint("CK_Bookings_NoRoomWhenInactive", "\"Status\" NOT IN ('New', 'Cancelled', 'NoShow') OR \"RoomId\" IS NULL");
                    table.CheckConstraint("CK_Bookings_RoomForStay", "\"Status\" NOT IN ('CheckedIn', 'Completed') OR \"RoomId\" IS NOT NULL");
                    table.CheckConstraint("CK_Bookings_Source", "\"Source\" IN ('Portal', 'Phone', 'Email', 'WalkIn', 'Agency')");
                    table.CheckConstraint("CK_Bookings_Status", "\"Status\" IN ('New', 'Confirmed', 'CheckedIn', 'Completed', 'Cancelled', 'NoShow')");
                    table.CheckConstraint("CK_Bookings_TotalPrice", "\"TotalPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_Bookings_CorporateClients_CorporateClientId",
                        column: x => x.CorporateClientId,
                        principalSchema: "guests",
                        principalTable: "CorporateClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Guests_GuestId",
                        column: x => x.GuestId,
                        principalSchema: "guests",
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalSchema: "rooms",
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_RoomCategories_RoomCategoryId",
                        column: x => x.RoomCategoryId,
                        principalSchema: "rooms",
                        principalTable: "RoomCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "rooms",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingHistory",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHistory", x => x.Id);
                    table.CheckConstraint("CK_BookingHistory_EventType", "\"EventType\" IN ('Created', 'FieldChanged', 'StatusChanged', 'PaymentRecorded')");
                    table.CheckConstraint("CK_BookingHistory_FieldName", "\"EventType\" NOT IN ('FieldChanged', 'StatusChanged') OR \"FieldName\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_BookingHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingHistory_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingNights",
                schema: "bookings",
                columns: table => new
                {
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Night = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNights", x => new { x.BookingId, x.Night });
                    table.CheckConstraint("CK_BookingNights_Price", "\"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_BookingNights_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HousekeepingTasks",
                schema: "housekeeping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<short>(type: "smallint", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousekeepingTasks", x => x.Id);
                    table.CheckConstraint("CK_HousekeepingTasks_AfterDepartureBooking", "\"Type\" <> 'AfterDeparture' OR \"BookingId\" IS NOT NULL");
                    table.CheckConstraint("CK_HousekeepingTasks_Assignee", "\"Status\" NOT IN ('Assigned', 'InProgress', 'Done') OR \"AssignedToUserId\" IS NOT NULL");
                    table.CheckConstraint("CK_HousekeepingTasks_Priority", "\"Priority\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_HousekeepingTasks_Status", "\"Status\" IN ('New', 'Assigned', 'InProgress', 'Done', 'Cancelled')");
                    table.CheckConstraint("CK_HousekeepingTasks_Type", "\"Type\" IN ('AfterDeparture', 'Routine', 'Deep')");
                    table.ForeignKey(
                        name: "FK_HousekeepingTasks_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HousekeepingTasks_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "rooms",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HousekeepingTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HousekeepingTasks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('billing.\"InvoiceNumbers\"')"),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorporateClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.CheckConstraint("CK_Invoices_Status", "\"Status\" IN ('Open', 'PartiallyPaid', 'Paid', 'Voided')");
                    table.CheckConstraint("CK_Invoices_VoidDetails", "\"Status\" <> 'Voided' OR (\"VoidedByUserId\" IS NOT NULL AND \"VoidReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_Invoices_VoidedAt", "(\"Status\" = 'Voided') = (\"VoidedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Invoices_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_CorporateClients_CorporateClientId",
                        column: x => x.CorporateClientId,
                        principalSchema: "guests",
                        principalTable: "CorporateClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceIssues",
                schema: "housekeeping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    HousekeepingTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceIssues", x => x.Id);
                    table.CheckConstraint("CK_MaintenanceIssues_Resolution", "(\"ResolvedAtUtc\" IS NULL) = (\"ResolvedByUserId\" IS NULL)");
                    table.CheckConstraint("CK_MaintenanceIssues_ResolvedAfterReported", "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"ReportedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_HousekeepingTasks_HousekeepingTaskId",
                        column: x => x.HousekeepingTaskId,
                        principalSchema: "housekeeping",
                        principalTable: "HousekeepingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "rooms",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, computedColumnSql: "\"Quantity\" * \"UnitPrice\"", stored: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceItems_Kind", "\"Kind\" IN ('Accommodation', 'Service')");
                    table.CheckConstraint("CK_InvoiceItems_OneNightPerLine", "\"Kind\" <> 'Accommodation' OR \"Quantity\" = 1");
                    table.CheckConstraint("CK_InvoiceItems_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_InvoiceItems_ServiceRef", "(\"Kind\" = 'Service') = (\"ServiceId\" IS NOT NULL)");
                    table.CheckConstraint("CK_InvoiceItems_UnitPrice", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "billing",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "rooms",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_Payments_Kind", "\"Kind\" IN ('Payment', 'Refund')");
                    table.CheckConstraint("CK_Payments_Method", "\"Method\" IN ('Cash', 'Card', 'BankTransfer')");
                    table.CheckConstraint("CK_Payments_Void", "(\"VoidedAtUtc\" IS NULL) = (\"VoidedByUserId\" IS NULL) AND (\"VoidedAtUtc\" IS NULL) = (\"VoidReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "billing",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Roles",
                columns: new[] { "Id", "Code", "IsHotelBound", "IsStaff", "Name" },
                values: new object[,]
                {
                    { new Guid("66666666-6666-6666-6666-666666666601"), "Guest", false, false, "Гість" },
                    { new Guid("66666666-6666-6666-6666-666666666602"), "Receptionist", true, true, "Оператор рецепції" },
                    { new Guid("66666666-6666-6666-6666-666666666603"), "ReservationAgent", false, true, "Оператор відділу бронювання" },
                    { new Guid("66666666-6666-6666-6666-666666666604"), "Housekeeper", true, true, "Покоївка" },
                    { new Guid("66666666-6666-6666-6666-666666666605"), "HeadHousekeeper", true, true, "Старша покоївка" },
                    { new Guid("66666666-6666-6666-6666-666666666606"), "HotelManager", true, true, "Менеджер готелю" },
                    { new Guid("66666666-6666-6666-6666-666666666607"), "Accountant", false, true, "Бухгалтер" },
                    { new Guid("66666666-6666-6666-6666-666666666608"), "Executive", false, true, "Керівництво мережі" },
                    { new Guid("66666666-6666-6666-6666-666666666609"), "Administrator", false, true, "Адміністратор системи" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeasonRates_Period",
                schema: "rooms",
                table: "SeasonRates",
                sql: "\"EndDate\" >= \"StartDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeasonRates_PricePerNight",
                schema: "rooms",
                table: "SeasonRates",
                sql: "\"PricePerNight\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rooms_Capacity",
                schema: "rooms",
                table: "Rooms",
                sql: "\"Capacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rooms_Status",
                schema: "rooms",
                table: "Rooms",
                sql: "\"Status\" IN ('Free', 'Reserved', 'Occupied', 'Dirty', 'OutOfOrder')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoomCategories_Capacity",
                schema: "rooms",
                table: "RoomCategories",
                sql: "\"Capacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rates_BasePricePerNight",
                schema: "rooms",
                table: "Rates",
                sql: "\"BasePricePerNight\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hotels_CancellationDeadlineDaysBefore",
                schema: "rooms",
                table: "Hotels",
                sql: "\"CancellationDeadlineDaysBefore\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hotels_Stars",
                schema: "rooms",
                table: "Hotels",
                sql: "\"Stars\" BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTokens_TokenHash",
                schema: "identity",
                table: "AccountTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountTokens_UserId_Purpose",
                schema: "identity",
                table: "AccountTokens",
                columns: new[] { "UserId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_HotelId_OccurredAtUtc",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "HotelId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                schema: "audit",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_OccurredAtUtc",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHistory_AuthorUserId",
                schema: "bookings",
                table: "BookingHistory",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHistory_BookingId_OccurredAtUtc",
                schema: "bookings",
                table: "BookingHistory",
                columns: new[] { "BookingId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingNights_Night",
                schema: "bookings",
                table: "BookingNights",
                column: "Night");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Availability",
                schema: "bookings",
                table: "Bookings",
                columns: new[] { "HotelId", "RoomCategoryId", "CheckInDate", "CheckOutDate" },
                filter: "\"Status\" IN ('Confirmed', 'CheckedIn')");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CorporateClientId",
                schema: "bookings",
                table: "Bookings",
                column: "CorporateClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_GuestId",
                schema: "bookings",
                table: "Bookings",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_HotelId_CheckInDate",
                schema: "bookings",
                table: "Bookings",
                columns: new[] { "HotelId", "CheckInDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Number",
                schema: "bookings",
                table: "Bookings",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RoomCategoryId",
                schema: "bookings",
                table: "Bookings",
                column: "RoomCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_CheckInDate",
                schema: "bookings",
                table: "Bookings",
                columns: new[] { "Status", "CheckInDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Bookings_RoomId_Active",
                schema: "bookings",
                table: "Bookings",
                column: "RoomId",
                unique: true,
                filter: "\"Status\" IN ('Confirmed', 'CheckedIn')");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateClients_Edrpou",
                schema: "guests",
                table: "CorporateClients",
                column: "Edrpou",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DocumentNumber",
                schema: "guests",
                table: "Guests",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_Email",
                schema: "guests",
                table: "Guests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_LastName",
                schema: "guests",
                table: "Guests",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_Phone",
                schema: "guests",
                table: "Guests",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_UserId",
                schema: "guests",
                table: "Guests",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingTasks_AssignedToUserId_Status",
                schema: "housekeeping",
                table: "HousekeepingTasks",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingTasks_BookingId",
                schema: "housekeeping",
                table: "HousekeepingTasks",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingTasks_CreatedByUserId",
                schema: "housekeeping",
                table: "HousekeepingTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingTasks_RoomId_Status",
                schema: "housekeeping",
                table: "HousekeepingTasks",
                columns: new[] { "RoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_HousekeepingTasks_RoomId_ActiveAfterDeparture",
                schema: "housekeeping",
                table: "HousekeepingTasks",
                column: "RoomId",
                unique: true,
                filter: "\"Type\" = 'AfterDeparture' AND \"Status\" IN ('New', 'Assigned', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_CreatedByUserId",
                schema: "billing",
                table: "InvoiceItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                schema: "billing",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_ServiceDate",
                schema: "billing",
                table: "InvoiceItems",
                column: "ServiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_ServiceId",
                schema: "billing",
                table: "InvoiceItems",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "UX_InvoiceItems_InvoiceId_ServiceDate",
                schema: "billing",
                table: "InvoiceItems",
                columns: new[] { "InvoiceId", "ServiceDate" },
                unique: true,
                filter: "\"Kind\" = 'Accommodation'");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CorporateClientId",
                schema: "billing",
                table: "Invoices",
                column: "CorporateClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                schema: "billing",
                table: "Invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VoidedByUserId",
                schema: "billing",
                table: "Invoices",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_BookingId_NotVoided",
                schema: "billing",
                table: "Invoices",
                column: "BookingId",
                unique: true,
                filter: "\"Status\" <> 'Voided'");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_HousekeepingTaskId",
                schema: "housekeeping",
                table: "MaintenanceIssues",
                column: "HousekeepingTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_ReportedByUserId",
                schema: "housekeeping",
                table: "MaintenanceIssues",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_ResolvedByUserId",
                schema: "housekeeping",
                table: "MaintenanceIssues",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceIssues_RoomId_Open",
                schema: "housekeeping",
                table: "MaintenanceIssues",
                column: "RoomId",
                unique: true,
                filter: "\"ResolvedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DeduplicationKey",
                schema: "notifications",
                table: "OutboxMessages",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "notifications",
                table: "OutboxMessages",
                column: "NextAttemptAtUtc",
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_RelatedEntityType_RelatedEntityId",
                schema: "notifications",
                table: "OutboxMessages",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedByUserId",
                schema: "billing",
                table: "Payments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                schema: "billing",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VoidedByUserId",
                schema: "billing",
                table: "Payments",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                schema: "identity",
                table: "RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "identity",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                schema: "identity",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                schema: "identity",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_Name",
                schema: "rooms",
                table: "Services",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_HotelId",
                schema: "identity",
                table: "Users",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                schema: "identity",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "BookingHistory",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "BookingNights",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "InvoiceItems",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "MaintenanceIssues",
                schema: "housekeeping");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "rooms");

            migrationBuilder.DropTable(
                name: "HousekeepingTasks",
                schema: "housekeeping");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Bookings",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "CorporateClients",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "Guests",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SeasonRates_Period",
                schema: "rooms",
                table: "SeasonRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SeasonRates_PricePerNight",
                schema: "rooms",
                table: "SeasonRates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rooms_Capacity",
                schema: "rooms",
                table: "Rooms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rooms_Status",
                schema: "rooms",
                table: "Rooms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoomCategories_Capacity",
                schema: "rooms",
                table: "RoomCategories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rates_BasePricePerNight",
                schema: "rooms",
                table: "Rates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hotels_CancellationDeadlineDaysBefore",
                schema: "rooms",
                table: "Hotels");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hotels_Stars",
                schema: "rooms",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "rooms",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "CancellationDeadlineDaysBefore",
                schema: "rooms",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancellationDeadlineTime",
                schema: "rooms",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                schema: "rooms",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CheckOutTime",
                schema: "rooms",
                table: "Hotels");

            migrationBuilder.DropSequence(
                name: "BookingNumbers",
                schema: "bookings");

            migrationBuilder.DropSequence(
                name: "InvoiceNumbers",
                schema: "billing");

            migrationBuilder.RenameTable(
                name: "SeasonRates",
                schema: "rooms",
                newName: "SeasonRates");

            migrationBuilder.RenameTable(
                name: "Rooms",
                schema: "rooms",
                newName: "Rooms");

            migrationBuilder.RenameTable(
                name: "RoomCategories",
                schema: "rooms",
                newName: "RoomCategories");

            migrationBuilder.RenameTable(
                name: "Rates",
                schema: "rooms",
                newName: "Rates");

            migrationBuilder.RenameTable(
                name: "Hotels",
                schema: "rooms",
                newName: "Hotels");
        }
    }
}
