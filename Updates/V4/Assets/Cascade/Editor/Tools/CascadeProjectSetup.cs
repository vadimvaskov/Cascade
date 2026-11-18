using UnityEditor;
using UnityEngine;

namespace Cascade.EditorTools
{
    /// <summary>Меню Cascade → Apply Project Settings: портретная ориентация и имя приложения.</summary>
    public static class CascadeProjectSetup
    {
        [MenuItem("Cascade/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.productName = "Cascade";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Cascade] Project settings applied: portrait, product name Cascade");
        }
    }
}
