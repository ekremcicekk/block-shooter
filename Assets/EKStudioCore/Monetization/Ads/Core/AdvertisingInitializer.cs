using UnityEngine;

namespace EKStudio.Monetization
{
    public class AdvertisingInitializer : MonoBehaviour
    {
        [Header("Settings")]
        public AdConfiguration Settings;
        public GameObject MockCanvasPrefab;

        [Header("Options")]
        public bool LoadAdOnStart = true;

        private void Start()
        {
            if (Settings == null)
            {
                Debug.LogError("[AdvertisingInitializer]: Settings is not assigned!");
                return;
            }

            AdvertisingSystem.Initialise(this, LoadAdOnStart);
        }
    }
}
