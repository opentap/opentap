//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using OpenTap.Package;
using LibGit2Sharp;
using System.IO;
using System.Diagnostics;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class GitVersionCalculatorTests
    {
        Signature me = new Signature("Me", "me@opentap.io", DateTime.Now);
        MergeOptions noFastForward = new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward };

        void commitChangesToReadme(Repository repo, string newContent, string commitMessage)
        {
            File.WriteAllText(repo.Info.WorkingDirectory + "readme.md", newContent);
            Commands.Stage(repo, repo.Info.WorkingDirectory + "readme.md");
            repo.Commit(commitMessage, me, me);
        }

        void commitChangesToGitVersionFile(Repository repo, string version, string commitMessage)
        {
            string filePath = Path.Combine(repo.Info.WorkingDirectory, ".gitversion");
            using (StreamWriter file = new StreamWriter(filePath))
            {
                file.WriteLine("version = " + version);
                file.WriteLine("beta branch = master");
            }
            Commands.Stage(repo, filePath);
            repo.Commit(commitMessage, me, me);
        }

        [DebuggerStepThrough]
        void verifyVersion(Repository repo, int major, int minor, int patch, string prerelease, string branchName = "")
        {
            verifyVersion(repo, repo.Head.Tip, major, minor, patch, prerelease, branchName);
        }

        [DebuggerStepThrough]
        void verifyVersion(Repository repo, Commit c, int major, int minor, int patch, string prerelease, string branchName = null)
        {
            var metadata = c.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
            if (!String.IsNullOrEmpty(branchName))
                metadata += "." + branchName;
            using (var calc = new GitVersionCalulator(repo.Info.WorkingDirectory))
            {
                SemanticVersion tv = calc.GetVersion();
                Assert.AreEqual(major, tv.Major, "Unexpected patch major number.");
                Assert.AreEqual(minor, tv.Minor, "Unexpected patch minor number.");
                Assert.AreEqual(patch, tv.Patch, "Unexpected patch version number.");
                Assert.AreEqual(prerelease, tv.PreRelease, "Unexpected prerelease.");
                Assert.AreEqual(metadata, tv.BuildMetadata, "Unexpected build metadata.");
            }
        }

        Repository repo;
        [SetUp]
        public void InitTestRepo()
        {
            string repoPath = Path.Combine(Path.GetTempPath(), "getgitversiontest");
            FileSystemHelper.DeleteDirectory(repoPath);
            Directory.CreateDirectory(repoPath);
            LibGit2Sharp.Repository.Init(repoPath);
            repo = new Repository(repoPath);
        }

        [TearDown]
        public void CleanupTestRepo()
        {
            string repoPath = repo.Info.WorkingDirectory;
            if (repo != null)
                repo.Dispose();
            FileSystemHelper.DeleteDirectory(repoPath);
        }

        [Test]
        public void GetGitVersion()
        {
            commitChangesToReadme(repo, "Hej", "Added Readme");

            verifyVersion(repo, 0, 0, 1, "beta.1");

            commitChangesToGitVersionFile(repo, "1.0.0", "Added .gitversion file with version = 1.0.0");

            verifyVersion(repo,1, 0, 0, "beta.1");


            var featureBranch = repo.CreateBranch("feature");
            Commands.Checkout(repo, featureBranch);
            commitChangesToReadme(repo, "Hej2", "Changed Readme on feature branch");

            verifyVersion(repo, 1, 0, 0, "alpha.1.1", featureBranch.FriendlyName);

            Commands.Checkout(repo, "master");
            repo.Merge(featureBranch, me, noFastForward);

            verifyVersion(repo, 1, 0, 0, "beta.2");

            commitChangesToReadme(repo, "Version 2", "Bumped version to 2.0");
            commitChangesToGitVersionFile(repo, "2.0.0", "Updated version to 2.0.0 in .gitversion file");

            verifyVersion(repo, 2, 0, 0, "beta.1");


            repo.CreateBranch("release2x");
            Commands.Checkout(repo, "release2x");

            verifyVersion(repo, 2, 0, 0, "rc");

            Commands.Checkout(repo, "master");

            verifyVersion(repo, 2, 0, 0, "beta.1");

            Commands.Checkout(repo, "release2x");
            repo.ApplyTag("v2.0.0", me, "Official 2.0.0 release");

            verifyVersion(repo, 2, 0, 0, null);

            Commands.Checkout(repo, "master");
            commitChangesToGitVersionFile(repo, "2.1.0", "Bumped version to 2.1");
            verifyVersion(repo, 2, 1, 0, "beta.1");
            featureBranch = repo.CreateBranch("feature2");
            Commands.Checkout(repo, featureBranch);
            commitChangesToReadme(repo, "Version 2.1 something", "Changed readme");
            verifyVersion(repo, 2, 1, 0, "alpha.1.1", featureBranch.FriendlyName);
            Commands.Checkout(repo, "master");
            repo.Merge(featureBranch, me, noFastForward);
            verifyVersion(repo, 2, 1, 0, "beta.2");
            Commands.Checkout(repo, "release2x");
            repo.Merge("master", me, noFastForward);


            // At this point we have created the following git history:
            // *   Merge branch 'master' into 'rc2x'
            // |\
            // | *   (master) Merge branch 'feature2'
            // | |\
            // | | * (feature2) Changed readme
            // | |/
            // | *   (tag: v2.1) Bumped version to 2.1
            // |/
            // *     (tag: v2.0, release2x) Bumped version to 2.0
            var commit210rc = repo.Head.Tip;
            verifyVersion(repo, 2, 1, 0, "rc.1");

            var hotfixBranch = repo.CreateBranch("hotfix");
            Commands.Checkout(repo, hotfixBranch);
            commitChangesToReadme(repo, "Version 2.1 hotfix", "Changed readme on hotfix branch.");
            Commands.Checkout(repo, "release2x");
            repo.Merge(hotfixBranch, me, noFastForward);

            // At this point we have created the following git history:
            // *   (rc2x) Merge branch 'hotfix' into 'rc2x'
            // |\
            // | *   (hotfix) Changed readme on hotfix branch.
            // |/
            // *   Merge branch 'master' into 'rc2x'
            // |\
            // | *   (master) Merge branch 'feature2'
            // | |\
            // | | * (feature2) Changed readme
            // | |/
            // | *   (tag: v2.1) Bumped version to 2.1
            // |/
            // *     (tag: v2.0, release2x) Bumped version to 2.0
            new GitVersionAction() { PrintLog = "10", RepoPath = repo.Info.WorkingDirectory }.Execute(new System.Threading.CancellationToken());
            verifyVersion(repo, 2, 1, 0, "rc.2");

            // Now we will try to go back to a previous commit (not the HEAD):
            // This is what GitLab CI does, so important for us to support

            //Commands.Checkout(repo, shortHash211alpha);
            //verifyVersion(2, 1, 0, "alpha.1.1", shortHash211alpha + ".hotfix");   // TODO: This fails and we should try to improve. See TODO in GitVersionCalulator 

            Commands.Checkout(repo, commit210rc);
            verifyVersion(repo,commit210rc, 2, 1, 0, "rc.1");
        }


        [TestCase(new[]// if the branch we are on is not the beta or release branch, the prerelease should be alpha
        {
            "version = 2.0",
            "beta branch = test",
            "release tag = NotMatching"
        }, "alpha.1")]
        [TestCase(new[]
        {
            "version = 2.0",
            "#beta branch = NotMatching",
            "release tag = NotMatching"
        }, "beta.1")]
        [TestCase(new[]
        {
            "version = 2.0.0",
            "beta branch = master",
            "release tag = NotMatching"
        }, "beta.1")]

        [TestCase(new[]// if there is a tag on a release branch
        {
            "version = 2.0.0",
            "release branch = master",
            @"release tag = v\d+\.\d+\.\d+"
        }, null)]
        [TestCase(new[] // if there is a tag on a branch that is not the release branch
        {
            "version = 2.0.0",
            "beta branch = master",
            @"release tag = v\d+.\d+.\d+"
        }, null)]
        public void GitVersionConfig(string[] lines, string prerelease)
        {
            string filePath = Path.Combine(repo.Info.WorkingDirectory, ".gitversion");
            File.WriteAllLines(filePath, lines);
            Commands.Stage(repo, filePath);
            repo.Commit("Test", me, me);
            repo.ApplyTag("v2.0.0",me,"Annotation");
            repo.CreateBranch("test");
            verifyVersion(repo, 2, 0, 0, prerelease,(prerelease ?? "").StartsWith("alpha") ? "master": "");
        }

        [Test]
        public void CommitToReleaseAgainWithoutBump()
        {
            commitChangesToGitVersionFile(repo,"1.0.0", "Added .gitversion file with version = 1.0.0");
            verifyVersion(repo, 1, 0, 0, "beta.1");
            repo.CreateBranch("release2x");
            Commands.Checkout(repo, "release2x");
            commitChangesToReadme(repo, "Version 1.0 hotfix", "Changed readme on release branch.");
            repo.ApplyTag("v1.0.0", me, "Official 1.0.0 release");
            verifyVersion(repo, 1, 0, 0,null);
            commitChangesToReadme(repo, "Version 1.0 hotfix 2", "Changed readme on release branch.");
            repo.ApplyTag("v1.0.1", me, "Official 1.0.0 release");
            verifyVersion(repo, 1, 0, 0, null);  // TODO: this is a bit of a weird behavior
        }

        [Test]
        public void ConfigFileAddedOnAlphaBranch()
        {
            commitChangesToReadme(repo, "Hello", "Added readme.");
            verifyVersion(repo, 0, 0, 1, "beta.1");
            repo.CreateBranch("feature");
            Commands.Checkout(repo, "feature");
            commitChangesToGitVersionFile(repo, "1.0.0", "Added .gitversion file with version = 1.0.0");
            verifyVersion(repo, 1, 0, 0, "alpha.1","feature");
            Commands.Checkout(repo, "master");
            repo.Merge("feature", me, noFastForward);
            verifyVersion(repo, 1, 0, 0, "beta.2");
        }
    }
}
