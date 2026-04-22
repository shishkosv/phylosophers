using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Orkestrator.Config;
using Orkestrator.Models;

namespace Orkestrator.Orchestration;

public sealed class RepetitionGuard
{
    private readonly double _threshold;

    public RepetitionGuard(IOptions<OrchestratorOptions> options)
    {
        _threshold = options.Value.SimilarityThreshold;
    }

    public bool IsTooSimilar(string candidate, IEnumerable<RoomMessage> recentMessages)
    {
        var candidateTerms = Tokenize(candidate);
        foreach (var message in recentMessages.TakeLast(3))
        {
            var score = Jaccard(candidateTerms, Tokenize(message.Text));
            if (score >= _threshold)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> Tokenize(string text)
        => Regex.Matches(text.ToLowerInvariant(), "[a-z]{3,}")
            .Select(m => m.Value)
            .ToHashSet();

    private static double Jaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Intersect(right).Count();
        var union = left.Union(right).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }
}
