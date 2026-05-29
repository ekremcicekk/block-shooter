using UnityEditor;
using UnityEngine;
using System.Linq;

namespace EKStudio.Monetization.EditorTools
{
    /// <summary>
    /// Automatically adds MODULE_LEVELPLAY scripting define symbol when LevelPlay SDK is installed
    /// Checks for both ironSource and Unity.Services.LevelPlay assemblies
    /// </summary>
    [InitializeOnLoad]
    public class LevelPlayDefineSymbolAdder
    {
        private const string LEVELPLAY_DEFINE = "MODULE_LEVELPLAY";
        
        // Check for both old (ironSource) and new (Unity.Services.LevelPlay) assembly names
        private static readonly string[] LEVELPLAY_ASSEMBLY_NAMES = new string[]
        {
            "Unity.Services.LevelPlay",
            "Unity.Services.LevelPlay.Editor",
            "IronSource",
            "IronSourceSDK"
        };

        static LevelPlayDefineSymbolAdder()
        {
            // Check if any LevelPlay assembly exists
            bool hasLevelPlayAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .Any(asm => LEVELPLAY_ASSEMBLY_NAMES.Contains(asm.GetName().Name));

            // Check for Unity LevelPlay define symbols (set by Unity when package is installed)
            // Note: Unity Ads Mediation adds LEVELPLAY_DEPENDENCIES_INSTALLED
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            bool hasLevelPlayDefine = defines.Contains("UNITY_SERVICES_LEVELPLAY") || 
                                     defines.Contains("LEVELPLAY_DEPENDENCIES_INSTALLED");

            // LevelPlay is considered installed if EITHER assembly OR Unity's define exists
            bool hasLevelPlay = hasLevelPlayAssembly || hasLevelPlayDefine;

            if (hasLevelPlay)
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

                    if (!defines.Contains(LEVELPLAY_DEFINE))
                    {
                        if (string.IsNullOrEmpty(defines))
                        {
                            defines = LEVELPLAY_DEFINE;
                        }
                        else
                        {
                            defines += ";" + LEVELPLAY_DEFINE;
                        }

                        PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
                    }
                }
                catch
                {
                    // Some target groups might throw exceptions, ignore them
                }
            }

            Debug.Log($"[LevelPlay] Added scripting define symbol: {LEVELPLAY_DEFINE}");
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

                    if (defines.Contains(LEVELPLAY_DEFINE))
                    {
                        var defineList = defines.Split(';').ToList();
                        defineList.Remove(LEVELPLAY_DEFINE);
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