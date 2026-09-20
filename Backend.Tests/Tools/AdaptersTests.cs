using Backend.Modules.Tools.Adapters;
using FluentAssertions;
using Xunit;

namespace Backend.Tests.Tools;

public class AdaptersTests
{
    [Fact]
    public void GenericAdapter_ReturnsConfiguredIdAndUrl()
    {
        var adapter = new GenericAdapter("plugin-1", "http://plugin.test");

        adapter.PluginId.Should().Be("plugin-1");
        adapter.GetAccessUrl().Should().Be("http://plugin.test");
    }

    [Fact]
    public void GiteaAdapter_WhenEnvVarNotSet_ReturnsDefaultUrl()
    {
        Environment.SetEnvironmentVariable("PLUGIN_GITEA_URL", null);
        var adapter = new GiteaAdapter();

        adapter.PluginId.Should().Be("gitea");
        adapter.GetAccessUrl().Should().Be("http://localhost:3001");
    }

    [Fact]
    public void GiteaAdapter_WhenEnvVarSet_ReturnsEnvVarUrl()
    {
        Environment.SetEnvironmentVariable("PLUGIN_GITEA_URL", "http://gitea.custom");
        try
        {
            var adapter = new GiteaAdapter();
            adapter.GetAccessUrl().Should().Be("http://gitea.custom");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLUGIN_GITEA_URL", null);
        }
    }

    [Fact]
    public void BudibaseAdapter_WhenEnvVarNotSet_ReturnsDefaultUrl()
    {
        Environment.SetEnvironmentVariable("PLUGIN_BUDIBASE_URL", null);
        var adapter = new BudibaseAdapter();

        adapter.PluginId.Should().Be("budibase");
        adapter.GetAccessUrl().Should().Be("http://localhost:3002");
    }

    [Fact]
    public void BudibaseAdapter_WhenEnvVarSet_ReturnsEnvVarUrl()
    {
        Environment.SetEnvironmentVariable("PLUGIN_BUDIBASE_URL", "http://budibase.custom");
        try
        {
            var adapter = new BudibaseAdapter();
            adapter.GetAccessUrl().Should().Be("http://budibase.custom");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLUGIN_BUDIBASE_URL", null);
        }
    }
}
