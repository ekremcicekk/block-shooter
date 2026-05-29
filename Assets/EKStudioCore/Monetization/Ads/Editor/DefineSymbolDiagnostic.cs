using UnityEditor;
using UnityEngine;
using System.Linq;

namespace EKStudio.Monetization.EditorTools
{
    /// <summary>
    /// Diagnostic tool to check current define symbols and assemblies
    /// Menu: Tools/EKStudio/Ad SDK Diagnostic
    /// </summary>
    public class DefineSymbolDiagnostic
    {
           [MenuItem("Tools/Ad SDK Diagnostic")]
        public static void CheckDefines()
        {
            Debug.Log("=== AD SDK DEFINE SYMBOL DIAGNOSTIC ===");
            
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            
            Debug.Log($"Current Build Target: {buildTargetGroup}");
            Debug.Log($"Current Defines: {defines}");
            Debug.Log("");
            
            // Check AdMob
            bool hasAdMobAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .Any(asm => asm.GetName().Name == "GoogleMobileAds");
            bool hasAdMobDefine = defines.Contains("MODULE_ADMOB");
            Debug.Log($"[AdMob] Assembly Found: {hasAdMobAssembly} | Define Set: {hasAdMobDefine}");
            
            // Check Unity Ads
            bool hasUnityAdsAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .Any(asm => asm.GetName().Name == "UnityEngine.Advertisements");
            bool hasUnityAdsDefine = defines.Contains("MODULE_UNITYADS");
            Debug.Log($"[Unity Ads] Assembly Found: {hasUnityAdsAssembly} | Define Set: {hasUnityAdsDefine}");
            
            // Check LevelPlay
            string[] levelPlayAssemblies = new string[] 
            { 
                "Unity.Services.LevelPlay", 
                "Unity.Services.LevelPlay.Editor", 
                "IronSource", 
                "IronSourceSDK" 
            };
            
            bool hasLevelPlayAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .Any(asm => levelPlayAssemblies.Contains(asm.GetName().Name));
            
            bool hasLevelPlayRelatedDefine = defines.Contains("UNITY_SERVICES_LEVELPLAY") || 
                                            defines.Contains("LEVELPLAY_DEPENDENCIES_INSTALLED");
            bool hasModuleLevelPlayDefine = defines.Contains("MODULE_LEVELPLAY");
            
            Debug.Log($"[LevelPlay] Assembly Found: {hasLevelPlayAssembly}");
            Debug.Log($"[LevelPlay] Unity Define (UNITY_SERVICES_LEVELPLAY or LEVELPLAY_DEPENDENCIES_INSTALLED): {hasLevelPlayRelatedDefine}");
            Debug.Log($"[LevelPlay] MODULE_LEVELPLAY Define Set: {hasModuleLevelPlayDefine}");
            
            // List all loaded assemblies with "Level", "Iron", or "Ads" in the name
            Debug.Log("");
            Debug.Log("=== RELEVANT ASSEMBLIES ===");
            var relevantAssemblies = System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm => 
                {
                    var name = asm.GetName().Name;
                    return name.Contains("Level") || 
                           name.Contains("Iron") || 
                           name.Contains("Ads") || 
                           name.Contains("Advertisement");
                })
                .Select(asm => asm.GetName().Name)
                .ToList();
            
            if (relevantAssemblies.Count > 0)
            {
                foreach (var asmName in relevantAssemblies)
                {
                    Debug.Log($"  - {asmName}");
                }
            }
            else
            {
                Debug.Log("  No relevant assemblies found!");
            }
            
            Debug.Log("");
            Debug.Log("=== RECOMMENDATIONS ===");
            
            if (hasAdMobAssembly && !hasAdMobDefine)
            {
                Debug.LogWarning("AdMob SDK is installed but MODULE_ADMOB define is not set. Restart Unity Editor!");
            }
            
            if (hasUnityAdsAssembly && !hasUnityAdsDefine)
            {
                Debug.LogWarning("Unity Ads SDK is installed but MODULE_UNITYADS define is not set. Restart Unity Editor!");
            }
            
            if (hasLevelPlayRelatedDefine && !hasModuleLevelPlayDefine)
            {
                Debug.LogWarning("LevelPlay dependencies are installed but MODULE_LEVELPLAY define is not set. Restart Unity Editor!");
            }
            
            if (!hasLevelPlayAssembly && hasLevelPlayRelatedDefine)
            {
                Debug.LogWarning("LEVELPLAY_DEPENDENCIES_INSTALLED define exists but LevelPlay assemblies not found. This is normal if using Unity Ads Mediation package.");
            }
            
            Debug.Log("=== END DIAGNOSTIC ===");
        }
    }
}

