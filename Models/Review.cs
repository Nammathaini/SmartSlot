public class Review
{
    public int Id { get; set; }

    public int ParkingSlotId { get; set; }
    public int BookingId { get; set; }

    public int Rating { get; set; }  // 1 to 5
    public string? Comment { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.Now;
}