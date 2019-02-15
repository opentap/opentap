//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using OpenTap.Package;
using LibGit2Sharp;
using System.IO;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class GitHelperTests
    {
        [Test]
        public void GetTapVersion()
        {
            string repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Signature me = new Signature("Me", "me@keysight.com", DateTime.Now);
            try
            {
                Directory.CreateDirectory(repoPath);
                LibGit2Sharp.Repository.Init(repoPath);
                using (Repository repo = new Repository(repoPath))
                {
                    void commitChangesToReadme(string newContent, string commitMessage)
                    {
                        File.WriteAllText(repoPath + "/readme.md", newContent);
                        Commands.Stage(repo, "readme.md");
                        repo.Commit(commitMessage, me, me);
                    }

                    void verifyVersion(int major, int minor, int patch, string prerelease, string metadata)
                    {
                        using (var calc = new GitVersionCalulator(repoPath))
                        {
                            SemanticVersion tv = calc.GetVersion();
                            Assert.AreEqual(major, tv.Major, "Unexpected patch major number.");
                            Assert.AreEqual(minor, tv.Minor, "Unexpected patch minor number.");
                            Assert.AreEqual(patch, tv.Patch, "Unexpected patch version number.");
                            Assert.AreEqual(prerelease, tv.PreRelease, "Unexpected prerelease.");
                            Assert.AreEqual(metadata, tv.BuildMetadata, "Unexpected build metadata.");
                        }
                    }

                    commitChangesToReadme("Hej", "Added Readme");
                    repo.ApplyTag("v1.0", me, "first 1.0 beta");

                    // At this point we have created the following git history:
                    // *   7473d1f - (HEAD -> master, tag: v1.0) Added Readme
                    var shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    verifyVersion(1,0,0,"beta", shortHash);

                    var featureBranch = repo.CreateBranch("feature");
                    Commands.Checkout(repo, featureBranch);
                    commitChangesToReadme("Hej2", "Changed Readme on feature branch");

                    // At this point we have created the following git history:
                    // *   8d54c74 - (feature) Changed Readme on feature branch
                    // |
                    // *   7473d1f - (HEAD -> master, tag: v1.0) Added Readme
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
#if MORE_SEMVER
                    verifyVersion(1, 0, 0, "alpha", $"{shortHash}.{featureBranch.FriendlyName}");
#else
                    verifyVersion(1, 0, 1, "alpha", $"{shortHash}.{featureBranch.FriendlyName}");
#endif
                    Commands.Checkout(repo, "master");
                    repo.Merge(featureBranch, me, new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward });

                    // At this point we have created the following git history:
                    // *   c4b9351 - (HEAD -> master) Merge branch 'feature'
                    // |\
                    // | * 8d54c74 - (feature) Changed Readme on feature branch
                    // |/
                    // *   7473d1f - (tag: v1.0) Added Readme
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    verifyVersion(1, 0, 2, "beta", shortHash);

                    commitChangesToReadme("Version 2", "Bumped version to 2.0");
                    repo.ApplyTag("v2.0", me, "first 2.0 beta");

                    // At this point we have created the following git history:
                    // *   (HEAD -> master, tag: v2.0) Bumped version to 2.0
                    // |
                    // *   Merge branch 'feature'
                    // |\
                    // | * (feature) Changed Readme on feature branch
                    // |/
                    // *   (tag: v1.0) Added Readme
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    verifyVersion(2, 0, 0, "beta", shortHash);


                    var rcBranch = repo.CreateBranch("rc2x");
                    Commands.Checkout(repo, rcBranch);

                    // At this point we have created the following git history:
                    // *   (HEAD -> rc2x, master, tag: v2.0) Bumped version to 2.0
                    // |
                    // ...
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    verifyVersion(2, 0, 0, "rc", shortHash);

                    Commands.Checkout(repo, "master");

                    // At this point we have created the following git history:
                    // *   (HEAD -> master, rc2x, tag: v2.0) Bumped version to 2.0
                    // |
                    // ...
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    verifyVersion(2, 0, 0, "beta", shortHash);

                    var releaseBranch = repo.CreateBranch("release2x");
                    Commands.Checkout(repo, releaseBranch);

                    // At this point we have created the following git history:
                    // *   (HEAD -> release2x, rc2x, master, tag: v2.0) Bumped version to 2.0
                    // |
                    // ...
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    verifyVersion(2, 0, 0, null, shortHash);

                    Commands.Checkout(repo, "master");
                    commitChangesToReadme("Version 2.1", "Bumped version to 2.1");
                    repo.ApplyTag("v2.1", me, "first 2.1 beta");
                    featureBranch = repo.CreateBranch("feature2");
                    Commands.Checkout(repo, featureBranch);
                    commitChangesToReadme("Version 2.1 something", "Changed readme");
                    Commands.Checkout(repo, "master");
                    repo.Merge(featureBranch, me, new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward });
                    Commands.Checkout(repo, rcBranch);
                    repo.Merge("master", me, new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward });


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
                    var shortHash212rc = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
#if MORE_SEMVER
                    verifyVersion(2, 1, 2, "rc", shortHash212rc);
#else
                    verifyVersion(2, 1, 3, "rc", shortHash212rc);
#endif

                    var hotfixBranch = repo.CreateBranch("hotfix");
                    Commands.Checkout(repo, hotfixBranch);
                    var shortHash211alpha = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
                    commitChangesToReadme("Version 2.1 hotfix", "Changed readme on hotfix branch.");
                    Commands.Checkout(repo, "rc2x");
                    repo.Merge(hotfixBranch, me, new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward });

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
                    shortHash = repo.Head.Tip.Sha.Substring(0, 8);  // (note: hashes will differ each time this is run)
#if MORE_SEMVER
                    verifyVersion(2, 1, 2, "rc.2", shortHash);
#else
                    verifyVersion(2, 1, 5, "rc", shortHash);
#endif

                    // Now we will try to go back to a previous commit (not the HEAD):
                    // This is what GitLab CI does, so important for us to support

                    Commands.Checkout(repo, shortHash211alpha);
#if MORE_SEMVER
                    verifyVersion(2, 1, 2, "alpha", shortHash211alpha + ".hotfix");                    
#else
                    verifyVersion(2, 1, 3, "alpha", shortHash211alpha + ".hotfix");
#endif

                    //Commands.Checkout(repo, shortHash212rc);
                    //verifyVersion(2, 1, 2, "rc", shortHash212rc); // TODO: This fails and we should try to improve. See TODO in GitVersionCalulator
                }
            }
            finally
            {
                FileSystemHelper.DeleteDirectory(repoPath);
            }
        }

    }
}
