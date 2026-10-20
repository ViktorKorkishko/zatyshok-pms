using System.Globalization;
using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>
/// Booking of a room category for a period (table 12.12). Status changes follow table 12.25,
/// and every method that changes the booking writes a <see cref="History"/> record (FR-17);
/// a call that changes nothing writes nothing.
///
/// Hotel, category, guest, corporate client and room belong to other modules, so the booking
/// keeps only their ids, and methods take the values a rule needs (the hotel's current date,
/// the room's category, night prices) instead of those entities (9.4.1, rule 2). The application
/// services of T5/T6 load the entities, lock the rate row (12.8.2) and change the room, the
/// invoice and the outbox in the same transaction.
/// </summary>
public class Booking
{
    // The collections change only through the methods below, which keep the invariants:
    // nights are exactly CheckInDate … CheckOutDate − 1, TotalPrice is their sum, and each
    // change leaves a history record. EF Core reads and writes these fields directly.
    private readonly List<BookingNight> _nights = new();
    private readonly List<BookingHistory> _history = new();

    // For EF Core.
    private Booking()
    {
    }

    /// <summary>Creates a booking in the New state with its nights and the Created history record (table 12.25).</summary>
    /// <param name="nights">Price of every night from check-in to the day before check-out (FR-08).</param>
    /// <param name="freeCancellationUntilUtc">From <c>Hotel.GetFreeCancellationUntilUtc</c>; a snapshot of the hotel rules (12.3.3).</param>
    public Booking(
        Guid hotelId,
        Guid roomCategoryId,
        Guid guestId,
        Guid? corporateClientId,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        int guestsCount,
        BookingSource source,
        string? wishes,
        IEnumerable<NightPrice> nights,
        DateTime freeCancellationUntilUtc,
        Guid? authorUserId,
        DateTime nowUtc)
    {
        EnsureGuestsCount(guestsCount);

        HotelId = hotelId;
        RoomCategoryId = roomCategoryId;
        GuestId = guestId;
        CorporateClientId = corporateClientId;
        GuestsCount = guestsCount;
        Source = source;
        Wishes = wishes;
        FreeCancellationUntilUtc = freeCancellationUntilUtc;
        CreatedAtUtc = nowUtc;
        SetNights(checkInDate, checkOutDate, nights);

        AddHistory(BookingHistoryEventType.Created, null, null, null, null, authorUserId, nowUtc);
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Booking number shown to the guest (UC-04, step 7); the database assigns it from the BookingNumbers sequence.</summary>
    public long Number { get; private set; }

    public Guid HotelId { get; private set; }

    /// <summary>Booked category; changes on a category change (FR-14) or an upgrade at check-in (UC-12 A1).</summary>
    public Guid RoomCategoryId { get; private set; }

    public Guid GuestId { get; private set; }

    public Guid? CorporateClientId { get; private set; }

    /// <summary>Set by the pre-assignment on the arrival day (FR-19) or at check-in; empty in New, Cancelled and NoShow.</summary>
    public Guid? RoomId { get; private set; }

    /// <summary>Calendar date of the hotel (NFR-20).</summary>
    public DateOnly CheckInDate { get; private set; }

    public DateOnly CheckOutDate { get; private set; }

    public int GuestsCount { get; private set; }

    public BookingStatus Status { get; private set; } = BookingStatus.New;

    public BookingSource Source { get; private set; }

    /// <summary>Sum of the night prices, kept for lists and sorting (FR-18, 12.1.2).</summary>
    public decimal TotalPrice { get; private set; }

    public string? Wishes { get; private set; }

    /// <summary>Until this moment the guest may cancel without the hotel (FR-15).</summary>
    public DateTime FreeCancellationUntilUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTime? CheckedInAtUtc { get; private set; }

    public DateTime? CheckedOutAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Concurrency token mapped to the PostgreSQL system column xmin (12.3).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<BookingNight> Nights => _nights;

    public IReadOnlyCollection<BookingHistory> History => _history;

    /// <summary>New → Confirmed (FR-11, FR-13). The caller re-checks availability under the rate lock first (12.8.2).</summary>
    public void Confirm(Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("підтвердити", BookingStatus.New);
        ChangeStatus(BookingStatus.Confirmed, null, authorUserId, nowUtc);
    }

    /// <summary>
    /// New or Confirmed → Cancelled (FR-15, UC-07). A guest may cancel only before
    /// <see cref="FreeCancellationUntilUtc"/>; an operator may cancel at any time before check-in
    /// but must give a reason. A pre-assigned room is removed from the booking: the caller reads
    /// <see cref="RoomId"/> before the call and releases that room (Reserved → Free).
    /// </summary>
    public void Cancel(string? reason, bool byGuest, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("скасувати", BookingStatus.New, BookingStatus.Confirmed);
        if (byGuest && nowUtc >= FreeCancellationUntilUtc)
        {
            throw new DomainException("Граничний термін безкоштовного скасування минув; для скасування зверніться до готелю.");
        }

        if (!byGuest && string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Оператор має вказати причину скасування.");
        }

        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ClearRoom(authorUserId, nowUtc);
        ChangeStatus(BookingStatus.Cancelled, CancellationReason, authorUserId, nowUtc);
    }

    /// <summary>
    /// Confirmed → NoShow (FR-15): the check-in date is before the hotel's current date.
    /// Run by the Worker, so the history author is the system (null). The caller releases a
    /// pre-assigned room, as for <see cref="Cancel"/>.
    /// </summary>
    public void MarkNoShow(DateOnly hotelToday, DateTime nowUtc)
    {
        EnsureStatus("позначити неявку", BookingStatus.Confirmed);
        if (CheckInDate >= hotelToday)
        {
            throw new DomainException("Неявку можна позначити лише після дня заїзду.");
        }

        ClearRoom(null, nowUtc);
        ChangeStatus(BookingStatus.NoShow, null, null, nowUtc);
    }

    /// <summary>
    /// Pre-assigns a room on the arrival day (FR-19). The caller passes the room's category and
    /// moves the room Free → Reserved; the unique index UX_Bookings_RoomId_Active rejects a room
    /// that another active booking holds.
    /// </summary>
    public void AssignRoom(Guid roomId, Guid roomCategoryId, DateOnly hotelToday, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("призначити номер", BookingStatus.Confirmed);
        if (CheckInDate != hotelToday)
        {
            throw new DomainException("Номер можна попередньо призначити лише в день заїзду.");
        }

        EnsureRoomCategory(roomCategoryId);
        if (RoomId is not null)
        {
            throw new DomainException("Бронюванню вже призначено номер; спершу зніміть призначення.");
        }

        SetRoom(roomId, null, authorUserId, nowUtc);
    }

    /// <summary>Removes the pre-assignment; the caller moves the room Reserved → Free (table 12.26).</summary>
    public void UnassignRoom(Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("зняти призначення номера", BookingStatus.Confirmed);
        if (RoomId is null)
        {
            throw new DomainException("Бронюванню не призначено номер.");
        }

        ClearRoom(authorUserId, nowUtc);
    }

    /// <summary>
    /// Changes the dates of a New or Confirmed booking (FR-14, UC-06). The caller checks
    /// availability without this booking under the rate lock and passes the prices of the new
    /// nights and the new cancellation deadline; the prepayment invoice, if any, is synced by Billing.
    /// </summary>
    public void ChangeDates(
        DateOnly checkInDate,
        DateOnly checkOutDate,
        IEnumerable<NightPrice> nights,
        DateTime freeCancellationUntilUtc,
        Guid? authorUserId,
        DateTime nowUtc)
    {
        EnsureStatus("змінити дати", BookingStatus.New, BookingStatus.Confirmed);
        if (checkInDate == CheckInDate && checkOutDate == CheckOutDate)
        {
            return;
        }

        // The pre-assignment is valid only on the arrival day (FR-19).
        if (RoomId is not null && checkInDate != CheckInDate)
        {
            throw new DomainException("Спершу зніміть попереднє призначення номера: воно діє лише в день заїзду.");
        }

        var oldCheckIn = CheckInDate;
        var oldCheckOut = CheckOutDate;
        var oldTotal = TotalPrice;
        SetNights(checkInDate, checkOutDate, nights);
        FreeCancellationUntilUtc = freeCancellationUntilUtc;

        RecordChange(nameof(CheckInDate), oldCheckIn, CheckInDate, null, authorUserId, nowUtc);
        RecordChange(nameof(CheckOutDate), oldCheckOut, CheckOutDate, null, authorUserId, nowUtc);
        RecordChange(nameof(TotalPrice), oldTotal, TotalPrice, null, authorUserId, nowUtc);
    }

    /// <summary>
    /// Changes the category of a New or Confirmed booking (FR-14). The caller locks the rate of
    /// the new category, checks its availability and passes the night prices of that category.
    /// </summary>
    public void ChangeCategory(Guid roomCategoryId, IEnumerable<NightPrice> nights, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("змінити категорію", BookingStatus.New, BookingStatus.Confirmed);
        if (roomCategoryId == RoomCategoryId)
        {
            return;
        }

        if (RoomId is not null)
        {
            throw new DomainException("Спершу зніміть попереднє призначення номера: він належить іншій категорії.");
        }

        var oldCategory = RoomCategoryId;
        var oldTotal = TotalPrice;
        SetNights(CheckInDate, CheckOutDate, nights);
        RoomCategoryId = roomCategoryId;

        RecordChange(nameof(RoomCategoryId), oldCategory, RoomCategoryId, null, authorUserId, nowUtc);
        RecordChange(nameof(TotalPrice), oldTotal, TotalPrice, null, authorUserId, nowUtc);
    }

    /// <summary>Changes the number of guests of a New or Confirmed booking (FR-14); the category capacity is checked by the validator.</summary>
    public void ChangeGuestsCount(int guestsCount, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("змінити кількість гостей", BookingStatus.New, BookingStatus.Confirmed);
        EnsureGuestsCount(guestsCount);

        var old = GuestsCount;
        GuestsCount = guestsCount;
        RecordChange(nameof(GuestsCount), old, GuestsCount, null, authorUserId, nowUtc);
    }

    /// <summary>Changes the wishes of a New or Confirmed booking (FR-14).</summary>
    public void ChangeWishes(string? wishes, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("змінити побажання", BookingStatus.New, BookingStatus.Confirmed);

        var old = Wishes;
        Wishes = string.IsNullOrWhiteSpace(wishes) ? null : wishes;
        RecordChange(nameof(Wishes), old, Wishes, null, authorUserId, nowUtc);
    }

    /// <summary>
    /// Check-in to a room of a higher category at the price of the booked one (UC-12 A1): the
    /// category changes, the night prices stay. Without this the occupied room would not count in
    /// the availability of its category. The caller locks the target category's rate and checks
    /// its availability first (12.8.2), then calls <see cref="CheckIn"/>.
    /// </summary>
    public void UpgradeCategory(Guid roomCategoryId, DateOnly hotelToday, string? comment, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("перевести у вищу категорію", BookingStatus.Confirmed);
        EnsureCheckInDay(hotelToday);
        if (roomCategoryId == RoomCategoryId)
        {
            throw new DomainException("Бронювання вже має цю категорію.");
        }

        if (RoomId is not null)
        {
            throw new DomainException("Спершу зніміть попереднє призначення номера заброньованої категорії.");
        }

        var oldCategory = RoomCategoryId;
        RoomCategoryId = roomCategoryId;
        RecordChange(nameof(RoomCategoryId), oldCategory, RoomCategoryId, comment, authorUserId, nowUtc);
    }

    /// <summary>
    /// Confirmed → CheckedIn (FR-19, UC-12): the check-in date is not later than the hotel's
    /// current date and the room is of the booked category. If a room was pre-assigned, it must be
    /// the same one. The caller has already checked the guest's document data (FR-23, figure 9.12),
    /// moves the room to Occupied and opens or syncs the invoice.
    /// </summary>
    public void CheckIn(Guid roomId, Guid roomCategoryId, DateOnly hotelToday, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("заселити", BookingStatus.Confirmed);
        EnsureCheckInDay(hotelToday);
        EnsureRoomCategory(roomCategoryId);
        if (RoomId is not null && RoomId != roomId)
        {
            throw new DomainException("Бронюванню попередньо призначено інший номер; спершу зніміть призначення.");
        }

        if (RoomId is null)
        {
            SetRoom(roomId, null, authorUserId, nowUtc);
        }

        CheckedInAtUtc = nowUtc;
        ChangeStatus(BookingStatus.CheckedIn, null, authorUserId, nowUtc);
    }

    /// <summary>
    /// Extends the stay of a checked-in guest (FR-21): adds the nights from the current check-out
    /// date to the new one at the current tariff. The caller checks the category availability under
    /// the rate lock and adds the invoice lines.
    /// </summary>
    public void Extend(DateOnly newCheckOutDate, IEnumerable<NightPrice> additionalNights, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("продовжити проживання", BookingStatus.CheckedIn);
        if (newCheckOutDate <= CheckOutDate)
        {
            throw new DomainException("Нова дата виїзду має бути пізнішою за поточну.");
        }

        var oldCheckOut = CheckOutDate;
        var oldTotal = TotalPrice;
        var allNights = _nights.Select(n => new NightPrice(n.Night, n.Price)).Concat(additionalNights).ToList();
        SetNights(CheckInDate, newCheckOutDate, allNights);

        RecordChange(nameof(CheckOutDate), oldCheckOut, CheckOutDate, null, authorUserId, nowUtc);
        RecordChange(nameof(TotalPrice), oldTotal, TotalPrice, null, authorUserId, nowUtc);
    }

    /// <summary>
    /// Early departure (UC-13 A1): removes the nights from the new check-out date on; the caller
    /// removes their invoice lines. The new check-out date is not earlier than the hotel's current
    /// date: the nights the guest has already stayed are not removed and not refunded.
    /// </summary>
    public void ShortenStay(DateOnly newCheckOutDate, DateOnly hotelToday, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("скоротити проживання", BookingStatus.CheckedIn);
        if (newCheckOutDate <= CheckInDate || newCheckOutDate >= CheckOutDate)
        {
            throw new DomainException("Нова дата виїзду має бути пізнішою за дату заїзду і ранішою за поточну дату виїзду.");
        }

        if (newCheckOutDate < hotelToday)
        {
            throw new DomainException("Нова дата виїзду не може бути ранішою за поточну дату готелю: прожиті ночі не скасовуються.");
        }

        var oldCheckOut = CheckOutDate;
        var oldTotal = TotalPrice;
        var keptNights = _nights.Where(n => n.Night < newCheckOutDate).Select(n => new NightPrice(n.Night, n.Price)).ToList();
        SetNights(CheckInDate, newCheckOutDate, keptNights);

        RecordChange(nameof(CheckOutDate), oldCheckOut, CheckOutDate, null, authorUserId, nowUtc);
        RecordChange(nameof(TotalPrice), oldTotal, TotalPrice, null, authorUserId, nowUtc);
    }

    /// <summary>Moves a checked-in guest to another room of the same category (FR-21, table 12.25); the caller changes both rooms.</summary>
    public void MoveToRoom(Guid roomId, Guid roomCategoryId, string? comment, Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("переселити", BookingStatus.CheckedIn);
        EnsureRoomCategory(roomCategoryId);
        if (roomId == RoomId)
        {
            throw new DomainException("Гість уже проживає в цьому номері.");
        }

        SetRoom(roomId, comment, authorUserId, nowUtc);
    }

    /// <summary>
    /// CheckedIn → Completed (FR-20, UC-13). The room stays on the booking for the stay history.
    /// The caller checks the invoice balance (a debt needs the manager's approval), closes the
    /// invoice, moves the room to Dirty and creates the cleaning task.
    /// </summary>
    public void CheckOut(Guid? authorUserId, DateTime nowUtc)
    {
        EnsureStatus("виселити", BookingStatus.CheckedIn);
        CheckedOutAtUtc = nowUtc;
        ChangeStatus(BookingStatus.Completed, null, authorUserId, nowUtc);
    }

    /// <summary>
    /// History record for a payment or refund in the booking's invoice (event PaymentRecorded,
    /// table 9.7). Allowed in any state: a prepayment comes before check-in, a debt may be paid
    /// after check-out and a refund after cancellation. The booking itself does not change.
    /// </summary>
    public void RecordPayment(decimal amount, string? comment, Guid? authorUserId, DateTime nowUtc)
    {
        if (amount <= 0)
        {
            throw new DomainException("Сума платежу має бути більшою за нуль.");
        }

        AddHistory(BookingHistoryEventType.PaymentRecorded, null, null, Format(amount), comment, authorUserId, nowUtc);
    }

    // Replaces the period and its nights and recalculates TotalPrice. Checks the invariant of
    // table 12.13: every night from check-in to the day before check-out, each exactly once.
    private void SetNights(DateOnly checkInDate, DateOnly checkOutDate, IEnumerable<NightPrice> nights)
    {
        if (checkOutDate <= checkInDate)
        {
            throw new DomainException("Дата виїзду має бути пізнішою за дату заїзду.");
        }

        var prices = new Dictionary<DateOnly, decimal>();
        foreach (var night in nights)
        {
            if (night.Price < 0)
            {
                throw new DomainException($"Ціна ночі {Format(night.Night)} не може бути від'ємною.");
            }

            if (!prices.TryAdd(night.Night, night.Price))
            {
                throw new DomainException($"Ніч {Format(night.Night)} указано двічі.");
            }
        }

        var nightCount = checkOutDate.DayNumber - checkInDate.DayNumber;
        if (prices.Count != nightCount || prices.Keys.Any(n => n < checkInDate || n >= checkOutDate))
        {
            throw new DomainException("Ночі бронювання мають збігатися з датами від заїзду до дня перед виїздом.");
        }

        // Rows of the nights that stay are kept (only the price may change), so EF Core updates
        // them instead of deleting a row and inserting another one with the same key.
        _nights.RemoveAll(n => !prices.ContainsKey(n.Night));
        foreach (var night in _nights)
        {
            night.Price = prices[night.Night];
        }

        var existing = _nights.Select(n => n.Night).ToHashSet();
        foreach (var (date, price) in prices.Where(p => !existing.Contains(p.Key)))
        {
            _nights.Add(new BookingNight(Id, date, price));
        }

        _nights.Sort((a, b) => a.Night.CompareTo(b.Night));
        CheckInDate = checkInDate;
        CheckOutDate = checkOutDate;
        TotalPrice = prices.Values.Sum();
    }

    private void SetRoom(Guid roomId, string? comment, Guid? authorUserId, DateTime nowUtc)
    {
        var old = RoomId;
        RoomId = roomId;
        RecordChange(nameof(RoomId), old, RoomId, comment, authorUserId, nowUtc);
    }

    // Cancelled and NoShow bookings must not hold a room (recommended CHECK of 12.3.3).
    private void ClearRoom(Guid? authorUserId, DateTime nowUtc)
    {
        if (RoomId is null)
        {
            return;
        }

        var old = RoomId;
        RoomId = null;
        RecordChange(nameof(RoomId), old, null, null, authorUserId, nowUtc);
    }

    private void ChangeStatus(BookingStatus status, string? comment, Guid? authorUserId, DateTime nowUtc)
    {
        var old = Status;
        Status = status;
        UpdatedAtUtc = nowUtc;
        AddHistory(BookingHistoryEventType.StatusChanged, nameof(Status), Format(old), Format(status), comment, authorUserId, nowUtc);
    }

    // A FieldChanged record if the value really changed.
    private void RecordChange(string fieldName, object? oldValue, object? newValue, string? comment, Guid? authorUserId, DateTime nowUtc)
    {
        if (Equals(oldValue, newValue))
        {
            return;
        }

        UpdatedAtUtc = nowUtc;
        AddHistory(BookingHistoryEventType.FieldChanged, fieldName, Format(oldValue), Format(newValue), comment, authorUserId, nowUtc);
    }

    private void AddHistory(
        BookingHistoryEventType eventType,
        string? fieldName,
        string? oldValue,
        string? newValue,
        string? comment,
        Guid? authorUserId,
        DateTime nowUtc)
        => _history.Add(new BookingHistory(Id, eventType, fieldName, oldValue, newValue, comment, authorUserId, nowUtc));

    // History values are culture-independent text: dates as 2026-10-12, money as 2500.00 (UC-17).
    private static string? Format(object? value) => value switch
    {
        null => null,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal money => money.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private void EnsureStatus(string action, params BookingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw DomainException.InvalidTransition("Бронювання", StatusName(Status), action);
        }
    }

    // State names of figure 6.1 and FR-16 for error messages (NFR-13).
    private static string StatusName(BookingStatus status) => status switch
    {
        BookingStatus.New => "нове",
        BookingStatus.Confirmed => "підтверджене",
        BookingStatus.CheckedIn => "гість заселений",
        BookingStatus.Completed => "завершене",
        BookingStatus.Cancelled => "скасоване",
        BookingStatus.NoShow => "неявка",
        _ => status.ToString(),
    };

    private void EnsureCheckInDay(DateOnly hotelToday)
    {
        if (CheckInDate > hotelToday)
        {
            throw new DomainException("Заселення можливе не раніше дати заїзду.");
        }

        if (hotelToday >= CheckOutDate)
        {
            throw new DomainException("Період бронювання вже минув.");
        }
    }

    private void EnsureRoomCategory(Guid roomCategoryId)
    {
        if (roomCategoryId != RoomCategoryId)
        {
            throw new DomainException("Категорія номера не збігається з категорією бронювання.");
        }
    }

    private static void EnsureGuestsCount(int guestsCount)
    {
        if (guestsCount < 1)
        {
            throw new DomainException("Кількість гостей має бути не меншою за 1.");
        }
    }
}
