//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Tap.Shared;

namespace OpenTap
{
    class AssemblyFinder
    {
        static TraceSource log = Log.CreateSource("AssemblyFinder");
        public void Invalidate()
        {
            lastSearch = DateTime.MinValue;
        }

        public bool IncludeDependencies = false;
        public bool OutputLog = true;

        public AssemblyFinder()
        {
            matching = new Memorizer<string, string[]>(x => allFiles.Where(y => Path.GetFileNameWithoutExtension(y) == x).ToArray());
        }

        public IEnumerable<string> DirectoriesToSearch = new List<string>();

        DateTime lastSearch = DateTime.MinValue;
        string[] allFiles = null;
        string[] allSearchFiles = null;
        Memorizer<string, string[]> matching;
        object syncLock = new object();
        public string[] FindAssemblies(string fileName)
        {
            SyncFiles();
            return matching.Invoke(fileName);
        }

        static bool StrEq(string a, string b) => string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);

        public string[] AllAssemblies()
        {
            SyncFiles();
            return allSearchFiles;
        }

        private struct SearchDir
        {
            public DirectoryInfo Info;
            public bool IgnorePlugins;

            public SearchDir(string dir, bool excludeFromSearch)
            {
                this.Info = new DirectoryInfo(dir);
                IgnorePlugins = excludeFromSearch;
            }
            public SearchDir(DirectoryInfo dir, bool excludeFromSearch)
            {
                this.Info = dir;
                IgnorePlugins = excludeFromSearch;
            }
        }

        /// <summary>
        /// Updates the dll file cache
        /// </summary>
        private void SyncFiles()
        {
            lock (syncLock)
            {
                if ((DateTime.Now - lastSearch) < TimeSpan.FromSeconds(8))
                    return;

                var sw = Stopwatch.StartNew();
                var files = new HashSet<string>(new PathUtils.PathComparer());
                var searchFiles = new HashSet<string>(new PathUtils.PathComparer());
                foreach (var search_dir in DirectoriesToSearch.ToHashSet(new PathUtils.PathComparer()))
                {
                    var dirToSearch = new Queue<SearchDir>();
                    dirToSearch.Enqueue(new SearchDir(search_dir, false));
                    while (dirToSearch.Any())
                    {
                        var dir = dirToSearch.Dequeue();
                        try
                        {
                            FileInfo[] filesInDir = dir.Info.GetFiles();
                            if (filesInDir.Any(x => StrEq(x.Name, ".OpenTapIgnore")))  // .OpenTapIgnore means we should ignore this folder and sub folders w.r.t. both Assembly resolution and Plugin searching
                                continue;

                            bool ignorePlugins = dir.IgnorePlugins;

                            foreach (var subDir in dir.Info.EnumerateDirectories())
                            {
                                if (StrEq(subDir.Name, "obj"))
                                    continue; // skip obj subfolder
                                var ignorePluginsInSubDir = dir.IgnorePlugins || StrEq(subDir.Name, "Dependencies");
                                if (IncludeDependencies)
                                    ignorePluginsInSubDir = false;
                                dirToSearch.Enqueue(new SearchDir(subDir, ignorePluginsInSubDir));
                            }

                            foreach (var file in filesInDir)
                            {
                                var ext = file.Extension;
                                if (false == (StrEq(ext, ".exe") || StrEq(ext, ".dll")))
                                    continue;
                                if (file.Name.Contains(".vshost."))
                                    continue;

                                files.Add(file.FullName);
                                if (!ignorePlugins)
                                    searchFiles.Add(file.FullName);
                            }
                        }
                        catch (Exception e)
                        {
                            if (OutputLog)
                            {
                                log.Error("Unable to enumerate directory '{0}': '{1}'", search_dir ?? "(null)", e.Message);
                                log.Debug(e);
                            }
                        }
                    }
                }

                allFiles = files.ToArray();
                allSearchFiles = searchFiles.ToArray();
                matching.InvalidateAll();
                lastSearch = DateTime.Now;
                if (OutputLog)
                    log.Debug(sw, "Found {0}/{1} assembly files.", searchFiles.Count, files.Count);
            }
        }
    }
}
