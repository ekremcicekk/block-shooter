using UnityEditor;
using UnityEngine;
using EKStudio.Monetization;
using System;
using System.Reflection;

// This script keeps GoogleMobileAdsSettings app IDs in sync with EKStudioCore AdConfiguration asset.
// All comments and debug logs are in English as per project rule.

namespace EKStudio.Editor
{
    [InitializeOnLoad]
    public static class AdMobAppIdSyncEditor
    {
        static AdMobAppIdSyncEditor()
        {
            EditorApplication.delayCall += SyncAdMobAppIds;
        }

        private static void SyncAdMobAppIds()
        {
            // Find all AdConfiguration assets in the project
            string[] guids = AssetDatabase.FindAssets("t:AdConfiguration");
            if (guids == null || guids.Length == 0)
                return;

            // Use the first AdConfiguration asset found
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<AdConfiguration>(path);
            if (config == null)
                return;

            var adMobSettings = config.AdMobSettings;
            if (adMobSettings == null)
                return;

            // Try to get GoogleMobileAdsSettings type via reflection
            var gmaType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            if (gmaType == null)
                return;

            // Call static LoadInstance()
            var loadInstance = gmaType.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (loadInstance == null)
                return;
            var gmaSettings = loadInstance.Invoke(null, null);
            if (gmaSettings == null)
                return;

            bool changed = false;
            var androidProp = gmaType.GetProperty("GoogleMobileAdsAndroidAppId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var iosProp = gmaType.GetProperty("GoogleMobileAdsIOSAppId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (androidProp != null && androidProp.GetValue(gmaSettings) as string != adMobSettings.AndroidAppId)
            {
                androidProp.SetValue(gmaSettings, adMobSettings.AndroidAppId);
                changed = true;
            }
            if (iosProp != null && iosProp.GetValue(gmaSettings) as string != adMobSettings.IOSAppId)
            {
                iosProp.SetValue(gmaSettings, adMobSettings.IOSAppId);
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty((UnityEngine.Object)gmaSettings);
                AssetDatabase.SaveAssets();
                Debug.Log("[AdMobAppIdSyncEditor] GoogleMobileAdsSettings app IDs updated from EKStudioCore AdConfiguration.");
            }
        }
    }
}
