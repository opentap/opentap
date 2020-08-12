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
    /// <summary>
    /// CLI sub command `tap sdk gitversion` that can calculate a version number based on the git history and a .gitversion file.
    /// </summary>
    [Display("gitversion", Group: "sdk", Description: "Calculate the semantic version number for a specific git commit.")]
    public class GitVersionAction : OpenTap.Cli.ICliAction
    {
        private static readonly TraceSource log = Log.CreateSource("GitVersion");

        /// <summary>
        /// Represents the --log command line argument which prints git log for the last n commits including version numbers for each commit.
        /// </summary>
        [CommandLineArgument("log",     Description = "Print the git log for the last <arg> commits including their semantic version number.")]
        public string PrintLog { get; set; }

        /// <summary>
        /// Represents an unnamed command line argument which specifies for which git ref a version should be calculated.
        /// </summary>
        [UnnamedCommandLineArgument("ref", Required = false)]
        public string Sha { get; set; }

        /// <summary>
        /// Represents the --replace command line argument which causes this command to replace all occurrences of $(GitVersion) in the specified file. Cannot be used together with --log.
        /// </summary>
        [CommandLineArgument("replace", Description = "Replace all occurrences of $(GitVersion) in the specified file\nwith the calculated semantic version number. It cannot be used with --log.")]
        public string ReplaceFile { get; set; }

        /// <summary>
        /// Represents the --fields command line argument which specifies the number of version fields to print/replace.
        /// </summary>
        [CommandLineArgument("fields",  Description = "Number of version fields to print/replace. The fields are: major, minor, patch,\n" +
                                                      "pre-release, and build metadata. E.g., --fields=2 results in a version number\n" +
                                                      "containing only the major and minor field. The default is 5 (all fields).")]
        public int FieldCount { get; set; }

        /// <summary>
        /// Represents the --dir command line argument which specifies the directory in which the git repository to use is located.
        /// </summary>
        [CommandLineArgument("dir",     Description = "Directory containing the git repository to calculate the version number from.")]
        public string RepoPath { get; set; }

        /// <summary>
        /// Constructs new action with default values for arguments.
        /// </summary>
        public GitVersionAction()
        {
            RepoPath = Directory.GetCurrentDirectory();
            FieldCount = 5;
        }

        /// <summary>
        /// Executes this action.
        /// </summary>
        /// <returns>Returns 0 to indicate success.</returns>
        public int Execute(CancellationToken cancellationToken)
        {
            string repositoryDir = RepoPath;
            while (!Directory.Exists(Path.Combine(repositoryDir, ".git")))
            {
                repositoryDir = Path.GetDirectoryName(repositoryDir);
                if (repositoryDir == null)
                {
                    log.Error("Directory {0} is not a git repository.", RepoPath);
                    return 1;
                }
            }
            RepoPath = repositoryDir;

            if (FieldCount < 1 || FieldCount > 5)
            {
                log.Error("The argument for --fields ({0}) must be an integer between 1 and 5.", FieldCount);
                return 1;
            }

            if (!String.IsNullOrEmpty(PrintLog))
            {
                int nLines = 0;
                if (!int.TryParse(PrintLog, out nLines) || nLines <= 0)
                {
                    log.Error("The argument for --log ({0}) must be an integer greater than 0.", PrintLog);
                    return 1;
                }
                return DoPrintLog(cancellationToken);
            }

            string versionString = null;
            using (GitVersionCalulator calc = new GitVersionCalulator(RepoPath))
            {
                if (String.IsNullOrEmpty(Sha))
                    versionString = calc.GetVersion().ToString(FieldCount);
                else
                    versionString = calc.GetVersion(Sha).ToString(FieldCount);
            }
            if (!String.IsNullOrEmpty(ReplaceFile))
            {
                if (!File.Exists(ReplaceFile))
                {
                    log.Error("File '{0}' given in --replace argument could not be found.", Path.GetFullPath(ReplaceFile));
                    return 1;
                }
                int replaceLineCount = DoReplaceFile(ReplaceFile, versionString);
                if (replaceLineCount == 0)
                    log.Warning("Nothing to replace. '$(GitVersion)' was not found in {0}.", ReplaceFile);
                else
                    log.Info("Replaced '$(GitVersion)' with '{0}' in {1} line(s) of {2}", versionString, replaceLineCount, ReplaceFile);
                return 0;
            }
            log.Info(versionString);
            return 0;
        }

        private static int DoReplaceFile(string fileName, string versionString)
        {
            int replaceLineCount = 0;
            using (var input = File.OpenText(fileName))
            using (var output = new StreamWriter(fileName + ".tmp"))
            {
                string line;
                while (null != (line = input.ReadLine()))
                {
                    if (line.Contains("$(GitVersion)"))
                    {
                        replaceLineCount++;
                        line = line.Replace("$(GitVersion)", versionString);
                    }
                    output.WriteLine(line);
                }
            }
            //File.Replace(fileName + ".tmp", fileName, fileName + ".org");
            File.Replace(fileName + ".tmp", fileName, null);
            return replaceLineCount;
        }

        private int DoPrintLog(CancellationToken cancellationToken)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            ConsoleColor graphColor = ConsoleColor.DarkYellow;
            ConsoleColor versionColor = ConsoleColor.DarkRed;

            using (GitVersionCalulator versionCalculater = new GitVersionCalulator(RepoPath))
            using (LibGit2Sharp.Repository repo = new LibGit2Sharp.Repository(RepoPath))
            {
                Commit tip = repo.Head.Tip;
                if (!string.IsNullOrEmpty(Sha))
                {
                    tip = repo.Lookup<Commit>(Sha);
                    if(tip == null)
                    {
                        log.Error($"The commit with reference {Sha} does not exist in the repository.");
                        return 1;
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

                    if (maxPosition < commitPosition[c])
                        maxPosition = commitPosition[c];

                    if(!c.Parents.Any())
                    {
                        // this is the very first commit in the repo. Stop here.
                        maxLines = ++lineCount;
                        break;
                    }
                    Commit p1 = c.Parents.First();
                    if (c.Parents.Count() > 1)
                    {
                        Commit p2 = c.Parents.Last();

                        if (commitPosition.ContainsKey(p1))
                            if (commitPosition[p1] != commitPosition[c])
                            {
                                int startPos = Math.Min(commitPosition[p1], commitPosition[c]);
                                int endPos = Math.Max(commitPosition[p1], commitPosition[c]);

                                commitPosition[c] = startPos;

                                foreach (var kvp in commitPosition.Where((KeyValuePair<Commit, int> kvp) => kvp.Value == endPos).ToList())
                                {
                                    commitPosition.Remove(kvp.Key);
                                }
                            }

                        if (!commitPosition.ContainsKey(p2))
                        {
                            // move out to an position out for the new branch
                            int newPosition = commitPosition[c] + 1;
                            while (commitPosition.ContainsValue(newPosition) &&
                                    (newPosition <= commitPosition.Values.Max()))
                                newPosition++;
                            commitPosition[p2] = newPosition;

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

                            // c is now merged back, no need to keep track of it (or any other commit on this branch)
                            // this way we can reuse the position for another branch 
                            foreach (var kvp in commitPosition.Where((KeyValuePair<Commit, int> kvp) => kvp.Value == endPos).ToList())
                            {
                                commitPosition.Remove(kvp.Key);
                            }
                            commitPosition[p1] = startPos;
                            foreach (var kvp in commitPosition.Where((KeyValuePair<Commit, int> kvp) => kvp.Value == startPos).ToList())
                            {
                                if(kvp.Key != p1)
                                    commitPosition.Remove(kvp.Key);
                            }
                        }
                    }
                    if (++lineCount >= maxLines)
                        break;
                }
                {
                    maxPosition++;
                }

                {
                    // Run through again to print
                    lineCount = 0;
                    commitPosition = new Dictionary<Commit, int>();
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
                        Console.Write(versionCalculater.GetVersion(c).ToString(FieldCount));
                        Console.ForegroundColor = defaultColor;

                        Console.Write(" - ");
                        Console.Write(c.MessageShort.Trim());

                        if (++lineCount >= maxLines)
                        {
                            Console.WriteLine();
                            break;
                        }

                        if (c.Parents.Any())
                        {
                            Commit p1 = c.Parents.First();
                            //Console.Write("Parent1: " + p1.Sha.Substring(0,8));
                            Console.WriteLine();
                            Console.ForegroundColor = graphColor;
                            if (c.Parents.Count() > 1)
                            {
                                Commit p2 = c.Parents.Last();

                                int startPos;
                                int endPos;

                                if (commitPosition.ContainsKey(p1))
                                    if (commitPosition[p1] != commitPosition[c])
                                    {
                                        startPos = Math.Min(commitPosition[p1], commitPosition[c]);
                                        // something we already printed has the current commit as its parent, draw the line to that commit now
                                        DrawPositionSpacer(0, startPos);
                                        // Draw ├─┘
                                        Console.Write("\u251C\u2500");
                                        endPos = Math.Max(commitPosition[p1], commitPosition[c]);
                                        DrawMergePositionSpacer(startPos + 1, endPos);
                                        Console.Write("\u2518 ");
                                        DrawPositionSpacer(endPos + 1, maxPosition);
                                        Console.WriteLine();
                                        commitPosition[c] = startPos;
                                        foreach (var kvp in commitPosition.Where((KeyValuePair<Commit, int> kvp) => kvp.Value == endPos).ToList())
                                        {
                                            commitPosition.Remove(kvp.Key);
                                        }
                                    }

                                if (!commitPosition.ContainsKey(p2))
                                {
                                    DrawPositionSpacer(0, commitPosition[c]);
                                    // move out to an position out for the new branch
                                    int newPosition = commitPosition[c] + 1;
                                    while (commitPosition.ContainsValue(newPosition) &&
                                            (newPosition <= commitPosition.Values.Max()))
                                        newPosition++;
                                    commitPosition[p2] = newPosition;

                                    commitPosition[p1] = commitPosition[c];
                                    // Draw ├─┐
                                    Console.Write("\u251C\u2500");
                                    DrawMergePositionSpacer(commitPosition[c] + 1, commitPosition[p2]);
                                    Console.Write("\u2510 ");
                                    DrawPositionSpacer(commitPosition[p2] + 1, maxPosition);
                                    Console.WriteLine();
                                }
                                else if (!commitPosition.ContainsKey(p1))
                                {
                                    commitPosition[p1] = commitPosition[c];
                                    // this branch is merged several times
                                    startPos = Math.Min(commitPosition[p2], commitPosition[c]);
                                    DrawPositionSpacer(0, startPos);
                                    // draws something like: ├─┤
                                    Console.Write("\u251C\u2500");
                                    endPos = Math.Max(commitPosition[p2], commitPosition[c]);
                                    DrawMergePositionSpacer(startPos + 1, endPos);
                                    Console.Write("\u2524 ");
                                    DrawPositionSpacer(endPos + 1, maxPosition);
                                    Console.WriteLine();
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
                                    foreach (var kvp in commitPosition.Where((KeyValuePair<Commit, int> kvp) => kvp.Value == endPos).ToList())
                                    {
                                        commitPosition.Remove(kvp.Key);
                                    }
                                    commitPosition[p1] = startPos;
                                    foreach (var kvp in commitPosition.Where((KeyValuePair<Commit, int> kvp) => kvp.Value == startPos).ToList())
                                    {
                                        if(kvp.Key != p1)
                                            commitPosition.Remove(kvp.Key);
                                    }

                                }
                            }
                        }
                    }
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// Defines the UseVersion XML element that can be used as a child element to the File element in package.xml 
    /// to indicate that a package should take its version from the AssemblyInfo in that file.
    /// </summary>
    [Display("UseVersion")]
    public class UseVersionData : ICustomPackageData
    {
    }
}
