using Xunit;

namespace Backend.Tests.Events;

// Tests in this collection all read/write the shared workflow-config.json file at the
// process's current directory, so they must not run concurrently with each other.
[CollectionDefinition("WorkflowConfigFile", DisableParallelization = true)]
public class WorkflowConfigFileCollection
{
}
