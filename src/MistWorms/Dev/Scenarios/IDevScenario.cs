#if MW_DEV
using System.Collections;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// An in-game self-test (spec §15.5). Implementations need a parameterless constructor; the runner
    /// discovers them by reflection. Name convention: "Sxx_Topic" (also addressable by its "Sxx" prefix).
    /// </summary>
    internal interface IDevScenario
    {
        string Name { get; }
        IEnumerator Run(ScenarioContext ctx);
    }
}
#endif
