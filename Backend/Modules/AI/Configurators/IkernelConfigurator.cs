using Microsoft.SemanticKernel;

namespace Backend.Modules.AI.Configurators;

public interface IKernelConfigurator
{
    string Provider { get; }
    void Configure(IKernelBuilder builder, IConfiguration configuration);
}