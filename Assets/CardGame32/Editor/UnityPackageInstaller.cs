using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace CardGame32Editor
{
    public static class UnityPackageInstaller
    {
        private static readonly string[] Packages =
        {
            "com.unity.services.authentication",
            "com.unity.services.vivox"
        };

        private static AddRequest request;
        private static int packageIndex;

        [MenuItem("CardGame32/Install Unity Multiplayer Service Packages")]
        public static void InstallUnityMultiplayerServicePackages()
        {
            packageIndex = 0;
            AddNextPackage();
        }

        public static void InstallUnityMultiplayerServicePackagesFromCommandLine()
        {
            InstallUnityMultiplayerServicePackages();
        }

        private static void AddNextPackage()
        {
            if (packageIndex >= Packages.Length)
            {
                Debug.Log("Finished installing Unity multiplayer service packages.");
                EditorApplication.Exit(0);
                return;
            }

            string packageName = Packages[packageIndex];
            Debug.Log("Installing package: " + packageName);
            request = Client.Add(packageName);
            EditorApplication.update += Progress;
        }

        private static void Progress()
        {
            if (!request.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= Progress;
            if (request.Status == StatusCode.Success)
            {
                Debug.Log("Installed package: " + request.Result.packageId);
                packageIndex++;
                AddNextPackage();
                return;
            }

            Debug.LogError("Failed to install package: " + Packages[packageIndex] + " " + request.Error.message);
            EditorApplication.Exit(1);
        }
    }
}
