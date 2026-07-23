using OpenTap;

namespace ProjectName.Tests;

public class PluginTests
{
    [Test]
    public void PluginAssemblyLoads()
    {
        var plugins = PluginManager.GetPlugins<ITestStep>();
        Assert.That(plugins, Is.Not.Null);
    }
}
