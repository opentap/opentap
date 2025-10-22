using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;


namespace OpenTap.UnitTests;

[TestFixture]
public class PluginSearcherThreadSafeTest
{
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(20)]
    [TestCase(40)]
    public void TestSearchAsync(int searchCount)
    {
        using var session = Session.Create(SessionOptions.RedirectLogging);
        var listener = new TestTraceListener();
        Log.AddListener(listener);
        var tasks = Enumerable.Range(0, searchCount).Select(_ => PluginManager.SearchAsync()).ToArray();
        Task.WaitAll(tasks);
        Assert.That(tasks.All(t => t.IsCompletedSuccessfully));
        Assert.That(listener.ErrorMessage, Is.Empty);
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(20)]
    [TestCase(40)]
    public void TestSearchSync(int searchCount)
    {
        using var session = Session.Create(SessionOptions.RedirectLogging);
        var listener = new TestTraceListener();
        Log.AddListener(listener);
        var tasks = Enumerable.Range(0, searchCount).Select(_ => TapThread.StartAwaitable(() => PluginManager.Search())).ToArray();
        Task.WaitAll(tasks);
        Assert.That(tasks.All(t => t.IsCompletedSuccessfully));
        Assert.That(listener.ErrorMessage, Is.Empty);
    }

    [TestCase(2)]
    [TestCase(6)]
    [TestCase(10)]
    [TestCase(20)]
    [TestCase(40)]
    public void TestSearchMixed(int searchCount)
    {
        using var session = Session.Create(SessionOptions.RedirectLogging);
        var listener = new TestTraceListener();
        Log.AddListener(listener);
        var tasks = Enumerable.Range(0, searchCount / 2).Select(_ => PluginManager.SearchAsync())
            .Concat(Enumerable.Range(0, searchCount / 2).Select(_ => TapThread.StartAwaitable(() => PluginManager.Search()))).ToArray();
        Task.WaitAll(tasks);
        Assert.That(tasks.All(t => t.IsCompletedSuccessfully));
        Assert.That(listener.ErrorMessage, Is.Empty);
    }
}
