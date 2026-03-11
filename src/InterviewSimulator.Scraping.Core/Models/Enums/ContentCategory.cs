namespace InterviewSimulator.Scraping.Core.Models.Enums;

/// <summary>
/// Categoría de contenido para el corpus RAG.
/// Cubre lenguajes, frameworks, conceptos de CS, áreas de desarrollo, etc.
/// </summary>
public enum ContentCategory
{
    // ── Lenguajes ──
    Java = 1,
    Python = 2,
    JavaScript = 3,
    TypeScript = 4,
    CSharp = 5,
    Go = 6,
    Rust = 7,
    PHP = 8,
    Ruby = 9,
    Swift = 10,
    Kotlin = 11,
    C = 12,
    Cpp = 13,

    // ── Frameworks ──
    React = 20,
    Angular = 21,
    VueJs = 22,
    NextJs = 23,
    NodeJs = 24,
    ExpressJs = 25,
    Django = 26,
    Flask = 27,
    SpringBoot = 28,
    AspNetCore = 29,
    DotNet = 30,
    EntityFramework = 31,
    Laravel = 32,
    Rails = 33,
    FastApi = 34,

    // ── Bases de datos ──
    Sql = 40,
    MySql = 41,
    PostgreSql = 42,
    MongoDb = 43,
    Redis = 44,
    ElasticSearch = 45,
    SqlServer = 46,
    SQLite = 47,

    // ── DevOps ──
    Docker = 50,
    Kubernetes = 51,
    Aws = 52,
    Azure = 53,
    Gcp = 54,
    CiCd = 55,
    Terraform = 56,
    Linux = 57,
    Git = 58,
    Nginx = 59,
    GitHubActions = 60,

    // ── Conceptos de Programación ──
    Oop = 70,
    FunctionalProgramming = 71,
    Solid = 72,
    DesignPatterns = 73,
    CleanCode = 74,
    Refactoring = 75,
    Testing = 76,
    Tdd = 77,

    // ── Arquitectura ──
    Microservices = 80,
    RestApi = 81,
    GraphQL = 82,
    SystemDesign = 83,
    CleanArchitecture = 84,
    EventDriven = 85,
    Cqrs = 86,
    Ddd = 87,

    // ── Estructuras de datos ──
    Arrays = 90,
    LinkedLists = 91,
    Trees = 92,
    Graphs = 93,
    HashTables = 94,
    Stacks = 95,
    Queues = 96,
    Heaps = 97,

    // ── Algoritmos ──
    Sorting = 100,
    Searching = 101,
    DynamicProgramming = 102,
    Greedy = 103,
    Recursion = 104,
    ComplexityAnalysis = 105,

    // ── Fundamentos CS ──
    OperatingSystems = 110,
    ComputerNetworks = 111,
    Dbms = 112,
    Concurrency = 113,
    MemoryManagement = 114,

    // ── Áreas ──
    Backend = 120,
    Frontend = 121,
    FullStack = 122,
    DevOps = 123,
    Database = 124,
    Cloud = 125,
    Mobile = 126,
    AiMl = 127,
    Security = 128,
    QaTesting = 129,

    // ── General / No clasificado ──
    General = 200,
    Unknown = 999
}
