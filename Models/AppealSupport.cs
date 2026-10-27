namespace CityFix_backend.Models
{
    /// <summary>A resident who reported "this affects me too" for someone else's appeal.</summary>
    public class AppealSupport
    {
        public int AppealId { get; set; }
        public Appeal? Appeal { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
