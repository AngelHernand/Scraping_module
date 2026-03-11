namespace InterviewSimulator.Scraping.Core.Models.Enums;

/// <summary>
/// Tipo de fuente web de donde se extrae contenido.
/// </summary>
public enum SourceType
{
    /// Dev.to, Medium, FreeCodeCamp
    BlogPlatform = 0,

    /// Glassdoor, Indeed
    JobBoard = 1,

    /// LeetCode, HackerRank
    CodingPlatform = 2,

    /// LinkedIn
    ProfessionalNetwork = 3,

    /// StackOverflow, Reddit
    Forum = 4,

    /// Microsoft Learn, MDN, Python Docs, PostgreSQL Docs
    OfficialDocumentation = 5,

    /// GeeksForGeeks, W3Schools, TutorialsPoint, JavaTPoint
    TechnicalEncyclopedia = 6,

    /// Platzi, KeepCoding, OpenWebinars, DigitalOcean
    EducationalPlatform = 7,

    /// GitHub repos con contenido Markdown
    GitHubRepository = 8,

    /// RefactoringGuru, MartinFowler, 12FactorApp
    ArchitectureReference = 9,

    Other = 99
}
