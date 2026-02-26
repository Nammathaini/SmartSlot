namespace SmartSlot.Models
{
    public class PasswordResetToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; } = false;
    }
}
