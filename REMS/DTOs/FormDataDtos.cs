using System.ComponentModel.DataAnnotations;

namespace REMS.DTOs;

public class FormDataRequest
{
    [StringLength(100)] public string? FirstName { get; set; }
    [StringLength(100)] public string? LastName { get; set; }
    [Range(0, 120)] public int Age { get; set; }
    [EmailAddress, StringLength(320)] public string? Email { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    [StringLength(500)] public string? FamiliarLanguages { get; set; }
    [StringLength(500)] public string? ProficientLanguages { get; set; }
    [StringLength(2000)] public string? LearningProblems { get; set; }
    [StringLength(150)] public string? Domain { get; set; }
    [StringLength(150)] public string? Major { get; set; }
    [StringLength(100)] public string? AcademicYear { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(100)] public string? ExpectedGradutionYear { get; set; }
    public bool ProgrammingAbility { get; set; }
    public bool TeamWorkAbility { get; set; }
    public bool IndividualTasksAbility { get; set; }
    public bool CleanCodeAbility { get; set; }
}

public sealed class FormDataUpdateRequest : FormDataRequest
{
    [Required] public int Id { get; set; }
}
