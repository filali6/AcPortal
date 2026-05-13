namespace Backend.Modules.AI.Configurators;

public class KernelConfiguratorFactory
{
    private readonly Dictionary<string, IKernelConfigurator> _configurators;

    public KernelConfiguratorFactory(IEnumerable<IKernelConfigurator> configurators)
    {
        _configurators = configurators.ToDictionary(c => c.Provider);
    }

    public IKernelConfigurator Get(string provider)
    {
        if (!_configurators.TryGetValue(provider.ToLower(), out var configurator))
            throw new InvalidOperationException($"Unsupported AI provider: {provider}");

        return configurator;
    }
}