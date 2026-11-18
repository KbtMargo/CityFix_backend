namespace CityFix_backend.Models
{
    public class AppealSupport
    {
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
