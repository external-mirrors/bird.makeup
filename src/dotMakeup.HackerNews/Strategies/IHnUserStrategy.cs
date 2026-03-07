#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8613, CS8618, CS8619, CS8620, CS8621, CS8625, CS8629, CS8631, CS8634
using dotMakeup.HackerNews.Models;

namespace dotMakeup.HackerNews.Strategies;

public interface IHnUserStrategy
{
    string Name { get; }
    int Priority { get; }
    Task<HNUser?> GetUserAsync(string username);
}
