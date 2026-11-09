namespace ReusableCaching.Models
{
    public class CacheSettings
    {
        public string Provider { get; set; } = "Memory";

        public int DurationMinutes { get; set; } = 60;

        public string InstanceName { get; set; } = Guid.NewGuid().ToString();
    }
}