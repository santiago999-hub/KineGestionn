using System.Collections.Generic;

namespace KineGestion.Core.DTOs
{
    public class OfficeClinicalProfileDto
    {
        public int OfficeId { get; set; }
        public IReadOnlyList<string> Professionals { get; set; } = new List<string>();
        public IReadOnlyList<string> Treatments { get; set; } = new List<string>();
        public IReadOnlyList<string> Equipments { get; set; } = new List<string>();
        public IReadOnlyList<string> ObrasSociales { get; set; } = new List<string>();
    }
}