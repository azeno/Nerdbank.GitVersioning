﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using NerdBank.GitVersioning;
using NerdBank.GitVersioning.Tests;
using Xunit;
using Xunit.Abstractions;

public class GitExtensionsTests : IDisposable
{
    private ITestOutputHelper logger;

    public GitExtensionsTests(ITestOutputHelper logger)
    {
        this.logger = logger;
        this.RepoPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(this.RepoPath);
        this.Repo = new Repository(Repository.Init(this.RepoPath));
    }

    public Repository Repo { get; set; }

    public string RepoPath { get; set; }

    public void Dispose()
    {
        this.Repo.Dispose();
        TestUtilities.DeleteDirectory(this.RepoPath);
    }

    [Fact]
    public void GetHeight_EmptyRepo()
    {
        Branch head = this.Repo.Head;
        Assert.Throws<InvalidOperationException>(() => head.GetHeight());
    }

    [Fact]
    public void GetHeight_SinglePath()
    {
        this.Repo.Commit("First", new CommitOptions { AllowEmptyCommit = true });
        this.Repo.Commit("Second", new CommitOptions { AllowEmptyCommit = true });
        Assert.Equal(2, this.Repo.Head.GetHeight());
    }

    [Fact]
    public void GetHeight_Merge()
    {
        var firstCommit = this.Repo.Commit("First", new CommitOptions { AllowEmptyCommit = true });
        var anotherBranch = this.Repo.CreateBranch("another");
        var secondCommit = this.Repo.Commit("Second", new CommitOptions { AllowEmptyCommit = true });
        this.Repo.Checkout(anotherBranch);
        for (int i = 1; i <= 5; i++)
        {
            this.Repo.Commit($"branch commit #{i}", new CommitOptions { AllowEmptyCommit = true });
        }

        this.Repo.Merge(secondCommit, new Signature("t", "t@t.com", DateTimeOffset.Now), new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastFoward });

        // While we've created 8 commits, the tallest height is only 7.
        Assert.Equal(7, this.Repo.Head.GetHeight());
    }

    [Fact]
    public void GetTruncatedCommitIdAsInteger_Roundtrip()
    {
        var firstCommit = this.Repo.Commit("First", new CommitOptions { AllowEmptyCommit = true });
        var secondCommit = this.Repo.Commit("Second", new CommitOptions { AllowEmptyCommit = true });

        int id1 = firstCommit.GetTruncatedCommitIdAsInteger();
        int id2 = secondCommit.GetTruncatedCommitIdAsInteger();

        this.logger.WriteLine($"Commit {firstCommit.Id.Sha.Substring(0, 8)} as int: {id1}");
        this.logger.WriteLine($"Commit {secondCommit.Id.Sha.Substring(0, 8)} as int: {id2}");

        Assert.Equal(firstCommit, this.Repo.GetCommitFromTruncatedIdInteger(id1));
        Assert.Equal(secondCommit, this.Repo.GetCommitFromTruncatedIdInteger(id2));
    }

    [Fact]
    public void GetIdAsVersion_ReadsMajorMinorFromVersionTxt()
    {
        this.StageVersionTxt(4, 8);
        var firstCommit = this.Repo.Commit("First");

        System.Version v1 = firstCommit.GetIdAsVersion();
        Assert.Equal(4, v1.Major);
        Assert.Equal(8, v1.Minor);
    }

    [Fact]
    public void GetIdAsVersion_Roundtrip()
    {
        StageVersionTxt(2, 5);
        var firstCommit = this.Repo.Commit("First");
        var secondCommit = this.Repo.Commit("Second", new CommitOptions { AllowEmptyCommit = true });

        System.Version v1 = firstCommit.GetIdAsVersion();
        System.Version v2 = secondCommit.GetIdAsVersion();

        this.logger.WriteLine($"Commit {firstCommit.Id.Sha.Substring(0, 8)} as version: {v1}");
        this.logger.WriteLine($"Commit {secondCommit.Id.Sha.Substring(0, 8)} as version: {v2}");

        Assert.Equal(firstCommit, this.Repo.GetCommitFromVersion(v1));
        Assert.Equal(secondCommit, this.Repo.GetCommitFromVersion(v2));
    }

    private void StageVersionTxt(int major, int minor)
    {
        string versionTextFilePath = Path.Combine(this.RepoPath, "version.txt");
        File.WriteAllText(versionTextFilePath, $"{major}.{minor}.0");
        this.Repo.Stage(versionTextFilePath);
    }
}
