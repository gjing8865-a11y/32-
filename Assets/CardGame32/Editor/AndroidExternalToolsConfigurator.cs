using UnityEditor;
using UnityEditor.Android;
using System.Reflection;

namespace CardGame32.Editor
{
    public static class AndroidExternalToolsConfigurator
    {
        private const string SdkPath = @"E:\UnityAndroidTools\SDK";
        private const string NdkPath = @"E:\UnityAndroidTools\SDK\ndk\27.2.12479018";
        private const string JdkPath = @"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK";

        [MenuItem("CardGame32/Configure Android External Tools")]
        public static void Configure()
        {
            AndroidExternalToolsSettings.sdkRootPath = SdkPath;
            AndroidExternalToolsSettings.ndkRootPath = NdkPath;
            AndroidExternalToolsSettings.jdkRootPath = JdkPath;

            SetOptionalBooleanPreference("SdkUseEmbedded", false);
            SetOptionalBooleanPreference("NdkUseEmbedded", false);
            SetOptionalBooleanPreference("JdkUseEmbedded", false);
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetString("AndroidSDKRoot", SdkPath);
            EditorPrefs.SetString("AndroidNDKRoot", NdkPath);
            EditorPrefs.SetString("AndroidJavaRoot", JdkPath);

            UnityEngine.Debug.Log($"Configured Android SDK: {SdkPath}");
            UnityEngine.Debug.Log($"Configured Android NDK: {NdkPath}");
            UnityEngine.Debug.Log($"Configured Android JDK: {JdkPath}");
        }

        public static void ConfigureFromCommandLine()
        {
            Configure();
        }

        private static void SetOptionalBooleanPreference(string propertyName, bool value)
        {
            var property = typeof(AndroidExternalToolsSettings).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
            {
                property.SetValue(null, value);
            }
        }
    }
}
