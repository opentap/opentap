//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using OpenTap.Cli;

namespace OpenTap.Package
{
    [Display("gitversion", Group: "sdk", Description: "Calculates a semantic version number for a specific git commit.")]
    public class GitVersionAction : OpenTap.Cli.ICliAction
    {
        [CommandLineArgument("dir", Description = "Directory containing git repository to calculate the version number from.")]
        public string RepoPath { get; set; }

        [CommandLineArgument("log", Description = "Print git log for the last n commits including version numbers for each commit.")]
        public string PrintLog { get; set; }

        [UnnamedCommandLineArgument("ref", Required = false)]
        public string Sha { get; set; }

        public GitVersionAction()
        {
            RepoPath = Directory.GetCurrentDirectory();
        }

        public int Execute(CancellationToken cancellationToken)
        {
            if (!String.IsNullOrEmpty(PrintLog))
            {
                DoPrintLog(cancellationToken);
                return 0;
            }
            using (GitVersionCalulator calc = new GitVersionCalulator(RepoPath))
            {
                if (String.IsNullOrEmpty(Sha))
                    Console.WriteLine(calc.GetVersion());
                else
                    Console.WriteLine(calc.GetVersion(Sha));
            }
            return 0;
        }

        private void DoPrintLog(CancellationToken cancellationToken)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            ConsoleColor graphColor = ConsoleColor.DarkYellow;
            ConsoleColor versionColor = ConsoleColor.DarkRed;
            string repositoryDir = RepoPath;
            while (!Directory.Exists(Path.Combine(repositoryDir, ".git")))
            {
                repositoryDir = Path.GetDirectoryName(repositoryDir);
                if (repositoryDir == null)
                    throw new ArgumentException("Directory is not a git repository.", "repositoryDir");
            }
            using (Repository repo = new Repository(repositoryDir))
            {
                Commit tip = repo.Head.Tip;
                if (!String.IsNullOrEmpty(Sha))
                {
                    repo.RevParse(Sha, out _, out GitObject obj);
                    if(obj is Commit c)
                    {
                        tip = c;
                    }
                }
                IEnumerable<Commit> History = repo.Commits.QueryBy(new CommitFilter() { IncludeReachableFrom = tip, SortBy = CommitSortStrategies.Topological });


                // Run through to determine max Position (number of concurrent branches) to be able to indent correctly later
                int maxLines = int.Parse(PrintLog);
                int lineCount = 0;
                Dictionary<Commit, int> commitPosition = new Dictionary<Commit, int>();
                commitPosition.Add(History.First(), 0);
                int maxPosition = 0;
                foreach (Commit c in History)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(!c.Parents.Any())
                    {
                        // this is the vary first commit in the repo. Stop here.
                        maxLines = lineCount;
                        break;
                    }
                    Commit p1 = c.Parents.First();
                    if (c.Parents.Count() > 1)
                    {
                        Commit p2 = c.Parents.Last();
                        if (!commitPosition.ContainsKey(p2))
                        {
                            // move out to an position out for the new branch
                            int newPosition = commitPosition[c] + 1;
                            while (newPosition <= commitPosition.Values.Max())
                                newPosition++;
                            commitPosition[p2] = newPosition;

                            if (!commitPosition.ContainsKey(p1))
                                commitPosition[p1] = commitPosition[c];
                        }
                        else if (!commitPosition.ContainsKey(p1))
                        {
                            commitPosition[p1] = commitPosition[c];
                        }
                    }
                    else
                    {
                        if (!commitPosition.ContainsKey(p1))
                            commitPosition[p1] = commitPosition[c];

                        if (commitPosition[p1] != commitPosition[c])
                        {
                            int startPos = Math.Min(commitPosition[p1], commitPosition[c]);
                            int endPos = Math.Max(commitPosition[p1], commitPosition[c]);

                            if (maxPosition < endPos)
                                maxPosition = endPos;
                            // c is now merged back, no need to keep track of it (or any other commit on this branch)
                            // this way we can reuse the position for another branch 
                            foreach (var kvp in commitPosition.Where(kvp => kvp.Value == endPos).ToList())
                            {
                                commitPosition.Remove(kvp.Key);
                            }
                            commitPosition[p1] = startPos;
                        }
                    }
                    if (lineCount++ > maxLines)
                        break;
                }
                {
                    int endMax = commitPosition.Values.Max();
                    if (maxPosition < endMax)
                        maxPosition = endMax;
                    maxPosition++;
                }

                using (GitVersionCalulator versionCalculater = new GitVersionCalulator(RepoPath))
                {
                    // Run through again to print
                    lineCount = 0;
                    commitPosition = new Dictionary<Commit, int>();
                    var longBranchOut = new List<Commit>();
                    commitPosition.Add(History.First(), 0);
                    HashSet<Commit> taggedCommits = repo.Tags.Select(t => t.Target.Peel<Commit>()).ToHashSet();
                    foreach (Commit c in History)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        void DrawPositionSpacer(int fromPos,int toPos)
                        {
                            for (int i = fromPos; i < toPos; i++)
                            {
                                if(commitPosition.ContainsValue(i))
                                    Console.Write("\u2502 ");
                                else
                                    Console.Write("  ");
                            }
                        }
                        void DrawMergePositionSpacer(int fromPos, int toPos)
                        {
                            for (int i = fromPos; i < toPos; i++)
                            {
                                if (commitPosition.ContainsValue(i))
                                    Console.Write("\u2502\u2500");
                                else
                                    Console.Write("\u2500\u2500");
                            }
                        }

                        foreach (Commit lb in longBranchOut.ToList())
                        {
                            if(lb.Parents.Contains(c))
                            {
                                if (commitPosition.ContainsKey(lb))
                                {
                                    if (commitPosition[lb] != commitPosition[c])
                                    {
                                        int startPos = Math.Min(commitPosition[lb], commitPosition[c]);
                                        // something we already printed has the current commit as its parent, draw the line to that commit now
                                        DrawPositionSpacer(0, startPos);
                                        // Draw ├─┘
                                        Console.Write("\u251C\u2500");
                                        int endPos = Math.Max(commitPosition[lb], commitPosition[c]);
                                        DrawMergePositionSpacer(startPos + 1, endPos);
                                        Console.Write("\u2518 ");
                                        DrawPositionSpacer(endPos + 1, maxPosition);
                                        Console.WriteLine();
                                    }
                                    commitPosition.Remove(lb);
                                }
                                longBranchOut.Remove(lb);
                            }
                        }
                        Console.ForegroundColor = graphColor;
                        DrawPositionSpacer(0, commitPosition[c]);
                        Console.ForegroundColor = defaultColor;
                        if (taggedCommits.Contains(c))
                            Console.Write("v ");
                        else
                            Console.Write("* ");
                        Console.ForegroundColor = graphColor;
                        DrawPositionSpacer(commitPosition[c] + 1, maxPosition);

                        Console.ForegroundColor = versionColor;
                        Console.Write(versionCalculater.GetVersion(c));
                        Console.ForegroundColor = defaultColor;

                        Console.Write(" - ");
                        Console.Write(c.MessageShort.Trim());
                        if (c.Parents.Any())
                        {
                            Commit p1 = c.Parents.First();
                            //Console.Write("Parent1: " + p1.Sha.Substring(0,8));
                            Console.WriteLine();
                            Console.ForegroundColor = graphColor;
                            if (c.Parents.Count() > 1)
                            {
                                Commit p2 = c.Parents.Last();
                                if (!commitPosition.ContainsKey(p2))
                                {
                                    DrawPositionSpacer(0, commitPosition[c]);
                                    // move out to an position out for the new branch
                                    int newPosition = commitPosition[c] + 1;
                                    while (newPosition <= commitPosition.Values.Max())
                                        newPosition++;
                                    commitPosition[p2] = newPosition;

                                    //if (!commitPosition.ContainsKey(p1))
                                        commitPosition[p1] = commitPosition[c];
                                    // Draw ├─┐
                                    Console.Write("\u251C\u2500");
                                    DrawMergePositionSpacer(commitPosition[c] + 1, commitPosition[p2]);
                                    Console.WriteLine("\u2510");
                                }
                                else if (!commitPosition.ContainsKey(p1))
                                {
                                    commitPosition[p1] = commitPosition[c];
                                    // this branch is merged several times
                                    int startPos = Math.Min(commitPosition[p2], commitPosition[c]);
                                    DrawPositionSpacer(0, startPos);
                                    // draws something like: ├─┤
                                    Console.Write("\u251C\u2500");
                                    DrawMergePositionSpacer(startPos + 1, Math.Max(commitPosition[p2], commitPosition[c]));
                                    Console.WriteLine("\u2524");
                                }
                                //else
                                //{
                                //    DrawPositionSpacer(0, commitPosition[p2]);
                                //    Console.Write("\u251C\u2500");
                                //    DrawMergePositionSpacer(commitPosition[p2] + 1, commitPosition[c]);
                                //    Console.WriteLine("\u2524");
                                //}
                            }
                            else
                            {
                                if (!commitPosition.ContainsKey(p1))
                                    commitPosition[p1] = commitPosition[c];

                                if (commitPosition[p1] != commitPosition[c])
                                {
                                    int startPos = Math.Min(commitPosition[p1], commitPosition[c]);
                                    DrawPositionSpacer(0, startPos);
                                    // Draw ├─┘
                                    Console.Write("\u251C\u2500");
                                    int endPos = Math.Max(commitPosition[p1], commitPosition[c]);
                                    DrawMergePositionSpacer(startPos + 1, endPos);
                                    Console.Write("\u2518 ");
                                    DrawPositionSpacer(endPos + 1, maxPosition);
                                    Console.WriteLine();
                                    // c is now merged back, no need to keep track of it (or any other commit on this branch)
                                    // this way we can reuse the position for another branch 
                                    foreach (var kvp in commitPosition.Where(kvp => kvp.Value == endPos).ToList())
                                    {
                                        commitPosition.Remove(kvp.Key);
                                    }
                                    commitPosition[p1] = startPos;
                                    foreach (var kvp in commitPosition.Where(kvp => kvp.Value == startPos).ToList())
                                    {
                                        if(kvp.Key != p1)
                                            commitPosition.Remove(kvp.Key);
                                    }

                                }
                                else
                                {
                                    longBranchOut.Add(c);
                                }
                            }
                        }
                        if (lineCount++ > maxLines)
                            break;
                    }
                }
            }
        }
    }

    [Display("UseVersion")]
    public class UseVersionData : ICustomPackageData
    {
    }
}
