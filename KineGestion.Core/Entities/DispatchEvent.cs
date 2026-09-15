using System;

namespace KineGestion.Core.Entities
{
    public class DispatchEvent
    {
        public int Id { get; set; }
        public string DispatchType { get; set; } = string.Empty;
        public int? SessionId { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
        public bool EmailSent { get; set; }
        public bool WhatsAppSent { get; set; }
        public string? Errors { get; set; }
    }
}