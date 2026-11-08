using System.IO;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    /// The real repo's art, loaded once for all studio tests.
    public static class StudioTestRepo
    {
        public static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static StudioData data;
        public static StudioData Data => data ??= StudioData.Load(Root);
    }
}
