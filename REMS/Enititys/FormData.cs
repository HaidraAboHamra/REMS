using System.ComponentModel.DataAnnotations;

namespace REMS.Enititys
{
    public class FormData
    {
        [Key]
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Age { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? FamiliarLanguages { get; set; }
        public string? ProficientLanguages { get; set; }
        public string? LearningProblems { get; set; }
        public string? Domain { get; set; }
        public string? Major { get; set; }
        public string? AcademicYear { get; set; }
        public string? Description { get; set; }
        public string? ExpectedGradutionYear { get; set; }
        public bool ProgrammingAbility { get; set; }
        public bool TeamWorkAbility { get; set; }
        public bool IndividualTasksAbility { get; set; }
        public bool CleanCodeAbility { get; set; }
    }
}
