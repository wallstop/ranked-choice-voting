using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RankedChoice
{
    /// <summary>
    /// Applies Ranked-Choice voting to some CSV file. Expected CSV file format is: 
    /// person1Vote1,person1Vote2,person1Vote3,...
    /// person2Vote1,person2Vote2,person2Vote3,...
    /// </summary>
    public sealed class Program
    {
        private static readonly string AppRoot = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        public static void Main(string[] args)
        {
            if (args?.Length != 1)
            {
                Console.WriteLine("Expected CSV file location as argument.");
                return;
            }

            string relativeCsvFilePath = args[0];
            string fullCsvFilePath = Path.Combine(AppRoot, relativeCsvFilePath);
            if (!File.Exists(fullCsvFilePath))
            {
                Console.WriteLine("Expected provided CSV file at {0} to exist, but it didn't.", fullCsvFilePath);
                return;
            }

            List<List<string>> voteRecords = File.ReadAllLines(fullCsvFilePath).Select(csvRow => csvRow.Split(','))
                .Select(voteRecord => voteRecord.Select(name => name?.ToLowerInvariant()).Distinct().ToList()).ToList();

            List<string> ranked = new List<string>();
            while (TryRank(voteRecords, out string best))
            {
                ranked.Add($"{ranked.Count + 1}: {best}");
                foreach (List<string> voteRecord in voteRecords)
                {
                    voteRecord?.Remove(best);
                }
            }

            Console.Out.WriteLine(string.Join("\n", ranked));
        }

        // Applies ranked-choice voting (https://ballotpedia.org/Ranked-choice_voting_(RCV)) to some set of ranked votes 
        private static bool TryRank(List<List<string>> votes, out string best)
        {
            if (votes is null || votes.Count == 0)
            {
                best = string.Empty;
                return false;
            }

            int ballots = 0;
            Dictionary<string, int> totaledVotes = new Dictionary<string, int>();
            foreach (List<string> orderedVotes in votes)
            {
                if (orderedVotes == null || orderedVotes.Count == 0)
                {
                    continue;
                }

                ++ballots;

                string bestVote = orderedVotes[0];
                if (!totaledVotes.TryGetValue(bestVote, out int currentCount))
                {
                    currentCount = 0;
                }

                ++currentCount;
                totaledVotes[bestVote] = currentCount;
            }

            if (ballots == 0)
            {
                best = string.Empty;
                return false;
            }

            float minVote = ballots / 2.0f;
            foreach (KeyValuePair<string, int> entry in totaledVotes)
            {
                if (minVote < entry.Value)
                {
                    best = entry.Key;
                    return true;
                }
            }

            string worstFirstRankedCandidate =
                // Deterministic worst-first-ranked
                totaledVotes.OrderBy(vote =>
                {
                    int voteCount = vote.Value;
                    // Rescale the vote's hash from range [0, int.MaxValue] into range [0.0, 1.0]
                    double fraction = Math.Abs(vote.Key.GetHashCode()) * 1.0 / int.MaxValue;
                    if (fraction < 0 || 1 < fraction)
                    {
                        throw new ArgumentException($"Unexpected fractional value {fraction} for vote {vote}");
                    }
                    return voteCount + fraction;
                }).Select(vote => vote.Key).FirstOrDefault();

            List<List<string>> modifiedVotes = votes.Select(ranked =>
            {
                if (ranked == null)
                {
                    return ranked;
                }

                ranked = ranked.ToList();
                ranked.RemoveAll(vote => string.Equals(vote, worstFirstRankedCandidate));
                return ranked;
            }).ToList();
            return TryRank(modifiedVotes, out best);
        }
    }
}