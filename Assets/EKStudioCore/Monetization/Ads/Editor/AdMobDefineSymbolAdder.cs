using UnityEditor;
using UnityEngine;
using System.Linq;

namespace EKStudio.Monetization.EditorTools
{
    /// <summary>
    /// Automatically adds MODULE_ADMOB scripting define symbol when AdMob SDK is installed
    /// </summary>
    [InitializeOnLoad]
    public class AdMobDefineSymbolAdder
    {
        private const string ADMOB_DEFINE = "MODULE_ADMOB";
        private const string ADMOB_ASSEMBLY_NAME = "GoogleMobileAds";

        static AdMobDefineSymbolAdder()
        {
            // Check if AdMob assembly exists
            bool hasAdMob = System.AppDomain.CurrentDomain.GetAssemblies()
                .Any(asm => asm.GetName().Name == ADMOB_ASSEMBLY_NAME);

            if (hasAdMob)
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

                    if (!defines.Contains(ADMOB_DEFINE))
                    {
                        if (string.IsNullOrEmpty(defines))
                        {
                            defines = ADMOB_DEFINE;
                        }
                        else
                        {
                            defines += ";" + ADMOB_DEFINE;
                        }

                        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
                    }
                }
                catch
                {
                    // Some target groups might throw exceptions, ignore them
                }
            }

            Debug.Log($"[AdMob] Added scripting define symbol: {ADMOB_DEFINE}");
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

                    if (defines.Contains(ADMOB_DEFINE))
                    {
                        var defineList = defines.Split(';').ToList();
                        defineList.Remove(ADMOB_DEFINE);
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
