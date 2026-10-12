using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AttackOnTroy.Editor.Setup
{
    public static class ConfigureAndroidBuildSettings
    {
        [MenuItem("Tools/Attack On Troy/Configure Android Build (Landscape + Android 13+)")]
        public static void Setup()
        {
            // 1. Orientation: Default Landscape Left, lock to Landscape Left & Right only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 2. Identification (Package Name & Company Name)
            PlayerSettings.companyName = "TroyStudio";
            PlayerSettings.productName = "gameTroy";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.troystudio.gametroy");

            // 3. Android Version & SDK (Tương thích Android 13+)
            // Min SDK: 26 (Android 8.0) cho phép mọi máy từ Android 8 đến Android 13, 14, 15+ đều cài được.
            // Target SDK: Automatic (Highest Installed) hoặc API 33/34
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // 4. Scripting Backend & Architecture
            // Android 13+ khuyến nghị 64-bit (ARM64) và IL2CPP
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildSetup] Đã chuẩn hóa toàn bộ cài đặt Build Android: Mặc định màn hình ngang (Landscape), IL2CPP/ARM64, Package Name hợp lệ.");
        }
    }
}
