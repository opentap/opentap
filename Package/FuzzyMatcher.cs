using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTap.Package
{
    internal class FuzzyMatcher
    {
        public class Match
        {
            public Match(string candidate, int score)
            {
                Candidate = candidate;
                Score = score;
            }
            public string Candidate { get; set; }
            public int Score { get; set; }
            public override string ToString()
            {
                return $"{Candidate}: {Score}";
            }
        }
        public string Input { get; }

        public FuzzyMatcher(string input, int maxThreshhold)
        {
            Input = preprocess(input);
            _maxThreshhold = maxThreshhold;
        }

        /// <summary>
        /// Remove all punctuation and whitespace from the input, and lowercase it
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string preprocess(string str)
        {
            var sb = new StringBuilder(str.Length);
            foreach (var ch in str)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
            }

            return sb.ToString().ToLower();
        }
        
        // Only consider candidates that are within an edit distance of 3 from the input
        // E.g. the input string can be transformed to the candidate with 3 changes (adding, removing, or replacing a character)
        private int _maxThreshhold;
        private int LevensteinDistance(string candidateString, string inputString, int x, int y, int score)
        {
            if (score > _maxThreshhold) return score;
            if (x == candidateString.Length) return score + inputString.Length - y;
            if (y == inputString.Length) return score + candidateString.Length - x;
            if (candidateString[x] == inputString[y]) return LevensteinDistance(candidateString, inputString, x + 1, y + 1, score);
            return Math.Min(
                LevensteinDistance(candidateString, inputString, x, y + 1, score + 1), // insert
                Math.Min(
                    LevensteinDistance(candidateString, inputString, x + 1, y, score + 1), // remove
                    LevensteinDistance(candidateString, inputString, x + 1, y + 1, score + 1))); // replace
        }
        
        /// <summary>
        /// Calculate how similar a candidate is to the input
        /// </summary>
        /// <param name="candidate"></param>
        /// <returns></returns>
        public Match Score(string candidate)
        {
            var score = 0;
            if (string.IsNullOrWhiteSpace(candidate)) return new Match(candidate, 0);
            var prep = preprocess(candidate);

            if (prep.Contains(Input))
            {
                if (prep == Input)
                    return new Match(candidate, 0);
                if (prep.StartsWith(Input) || prep.EndsWith(Input))
                    return new Match(candidate, 1);
                return new Match(candidate, 2);
            }

            // If the input is a prefix of the package name, it is considered a very good candidate
            if (prep.StartsWith(Input)) return new Match(candidate, 0);
            // If the input is a substring of the package name, it is a pretty good candiate
            if (prep.Contains(Input)) return new Match(candidate, 1);
            // Otherwise, the suitability is determined by the edit distance
            score = LevensteinDistance(prep, Input, 0, 0, 0);
            return new Match(candidate, score);
        }
    }
}