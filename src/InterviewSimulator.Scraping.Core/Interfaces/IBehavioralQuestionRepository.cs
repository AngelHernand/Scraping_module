using InterviewSimulator.Scraping.Core.Models;

namespace InterviewSimulator.Scraping.Core.Interfaces;

public interface IBehavioralQuestionRepository
{
    Task<List<BehavioralQuestion>> GetRandomQuestionsAsync(int count, string? difficulty = null);
    Task<BehavioralQuestion?> GetByIdAsync(int id);
    Task<int> GetTotalCountAsync();
    Task<List<string>> GetAvailableCategoriesAsync();
    Task SeedFromJsonAsync(string jsonContent);
}
