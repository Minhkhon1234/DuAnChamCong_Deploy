namespace DUANCHAMCONG.Models
{
    public class SchoolConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Radius { get; set; }
    }
}
