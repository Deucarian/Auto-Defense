using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.AutoDefense.PlayModeTests
{
    public sealed class AutoDefensePlayModeTests
    {
        [UnityTest]
        public IEnumerator BasicAutoDefenseSampleFrameRuns()
        {
            GameObject marker = new GameObject("auto-defense-playmode-marker");
            yield return null;
            Assert.NotNull(marker);
            Object.Destroy(marker);
        }
    }
}
