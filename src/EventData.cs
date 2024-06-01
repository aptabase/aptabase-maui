namespace Aptabase.Maui
{
    internal class EventData
    {
        public string Timestamp { get; set; }
        public string EventName { get; set; }
        public Dictionary<string, object>? Props { get; set; }
        public SystemInfo? SystemProps { get; set; }
        public string? SessionId { get; set; }

        public EventData(string eventName, Dictionary<string, object>? props = null)
        {
            Timestamp = DateTime.UtcNow.ToString("o");
            EventName = eventName;
            Props = props;
        }
    }
}
