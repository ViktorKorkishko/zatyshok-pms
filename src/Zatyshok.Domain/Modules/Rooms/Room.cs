using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>
/// A physical room of a hotel. The number is unique within the hotel (FR-06).
/// <see cref="Status"/> and <see cref="IsActive"/> change only through the methods below,
/// which follow table 12.26; the related booking, task and fault records are changed by
/// the application services of T3/T6 in the same transaction.
/// </summary>
public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomCategoryId { get; set; }

    public RoomCategory Category { get; set; } = null!;

    /// <summary>Room number as printed on the door, e.g. "305". Unique per hotel.</summary>
    public required string Number { get; set; }

    public int Floor { get; set; }

    /// <summary>Actual capacity of this room; may differ from the category default.</summary>
    public int Capacity { get; set; }

    /// <summary>Comma-separated amenity list; a dedicated dictionary is out of scope for the first version.</summary>
    public string? Amenities { get; set; }

    /// <summary>Notes, including a fault found in an occupied room (UC-16 A1).</summary>
    public string? Notes { get; set; }

    public RoomStatus Status { get; private set; } = RoomStatus.Free;

    /// <summary>Rooms with stay history are deactivated instead of deleted (UC-08).</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Free → Reserved: pre-assignment to a booking arriving today (FR-19). Date and category are checked by <c>Booking.AssignRoom</c>.</summary>
    public void Reserve()
    {
        EnsureActive("попередньо призначити");
        EnsureStatus("попередньо призначити", RoomStatus.Free);
        Status = RoomStatus.Reserved;
    }

    /// <summary>Reserved → Free: the booking was cancelled, became a no-show or the operator removed the pre-assignment.</summary>
    public void Release()
    {
        EnsureStatus("зняти призначення", RoomStatus.Reserved);
        Status = RoomStatus.Free;
    }

    /// <summary>Free or Reserved → Occupied: check-in (UC-12).</summary>
    public void Occupy()
    {
        EnsureActive("заселити");
        EnsureStatus("заселити", RoomStatus.Free, RoomStatus.Reserved);
        Status = RoomStatus.Occupied;
    }

    /// <summary>Occupied → Dirty: check-out (UC-13).</summary>
    public void MarkDirty()
    {
        EnsureStatus("позначити як такий, що потребує прибирання", RoomStatus.Occupied);
        Status = RoomStatus.Dirty;
    }

    /// <summary>
    /// Dirty → Free: the cleaning task is done (FR-29). A Dirty room never has an open fault
    /// (OutOfOrder ⇔ open fault, table 12.18), so no extra check is needed here.
    /// </summary>
    public void MarkClean()
    {
        EnsureStatus("позначити прибраним", RoomStatus.Dirty);
        Status = RoomStatus.Free;
    }

    /// <summary>Free or Dirty → OutOfOrder: a fault is registered (FR-30, UC-16).</summary>
    public void MarkOutOfOrder()
    {
        if (Status == RoomStatus.Occupied)
        {
            throw new DomainException(
                "Несправність у зайнятому номері записується в примітки номера; стан зміниться після виселення (UC-16 А1).");
        }

        if (Status == RoomStatus.Reserved)
        {
            throw new DomainException("Спершу зніміть попереднє призначення номера.");
        }

        EnsureStatus("вивести з експлуатації", RoomStatus.Free, RoomStatus.Dirty);
        Status = RoomStatus.OutOfOrder;
    }

    /// <summary>OutOfOrder → Dirty: the fault is fixed; a deep cleaning task follows (FR-30).</summary>
    public void ReturnToService()
    {
        EnsureStatus("повернути в експлуатацію", RoomStatus.OutOfOrder);
        Status = RoomStatus.Dirty;
    }

    /// <summary>Excludes the room from availability (UC-08). Independent of the state, but forbidden for an occupied room (UC-08 A2).</summary>
    public void Deactivate()
    {
        if (Status == RoomStatus.Occupied)
        {
            throw new DomainException("Зайнятий номер не можна деактивувати (UC-08 А2).");
        }

        IsActive = false;
    }

    /// <summary>Returns a deactivated room to the inventory; no state precondition.</summary>
    public void Activate() => IsActive = true;

    private void EnsureActive(string action)
    {
        if (!IsActive)
        {
            throw new DomainException($"Номер деактивовано: дію «{action}» заборонено.");
        }
    }

    private void EnsureStatus(string action, params RoomStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw DomainException.InvalidTransition("Номер", Status, action);
        }
    }
}
