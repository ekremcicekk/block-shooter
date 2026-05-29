using UnityEditor;
using UnityEngine;
using System.Linq;

namespace EKStudio.Monetization.EditorTools
{
    /// <summary>
    /// Automatically adds MODULE_UNITYADS scripting define symbol when Unity Ads package is installed
    /// </summary>
    [InitializeOnLoad]
    public class UnityAdsDefineSymbolAdder
    {
        private const string UNITYADS_DEFINE = "MODULE_UNITYADS";
        private const string UNITYADS_ASSEMBLY_NAME = "UnityEngine.Advertisements";

        static UnityAdsDefineSymbolAdder()
        {
            // Check if Unity Ads assembly exists
            bool hasUnityAds = System.AppDomain.CurrentDomain.GetAssemblies()
                .Any(asm => asm.GetName().Name == UNITYADS_ASSEMBLY_NAME);

            if (hasUnityAds)
            {
                AddDefineSymbol();
            }
            else
            {
                RemoveDefineSymbol();
            }
        }

        private static void AddDefineSymbol()
        {
            // Add define for all build target groups (Android, iOS, Standalone, etc.)
            foreach (BuildTargetGroup targetGroup in System.Enum.GetValues(typeof(BuildTargetGroup)))
            {
                // Skip obsolete and unknown target groups
                if (targetGroup == BuildTargetGroup.Unknown || IsObsolete(targetGroup))
                    continue;

                try
                {
                    var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

                    if (!defines.Contains(UNITYADS_DEFINE))
                    {
                        if (string.IsNullOrEmpty(defines))
                        {
                            defines = UNITYADS_DEFINE;
                        }
                        else
                        {
                            defines += ";" + UNITYADS_DEFINE;
                        }

                        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
                    }
                }
                catch
                {
                    // Some target groups might throw exceptions, ignore them
                }
            }

            Debug.Log($"[UnityAds] Added scripting define symbol: {UNITYADS_DEFINE}");
        }

        private static void RemoveDefineSymbol()
        {
            // Remove define from all build target groups
            foreach (BuildTargetGroup targetGroup in System.Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (targetGroup == BuildTargetGroup.Unknown || IsObsolete(targetGroup))
                    continue;

                try
                {
                    var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

                    if (defines.Contains(UNITYADS_DEFINE))
                    {
                        var defineList = defines.Split(';').ToList();
                        defineList.Remove(UNITYADS_DEFINE);
                        defines = string.Join(";", defineList);

                        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
                    }
                }
                catch
                {
                    // Ignore exceptions
                }
            }

        }

        private static bool IsObsolete(BuildTargetGroup group)
        {
            var attrs = typeof(BuildTargetGroup)
                .GetField(group.ToString())
                ?.GetCustomAttributes(typeof(System.ObsoleteAttribute), false);
            return attrs != null && attrs.Length > 0;
        }
    }
}

