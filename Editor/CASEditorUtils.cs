﻿using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;

namespace CAS.UEditor
{
    internal static class CASEditorUtils
    {
        #region Constants
        public const string logTag = "[CleverAdsSolutions] ";
        public const string packageName = "com.cleversolutions.ads.unity";
        public const string androidAdmobSampleAppID = "ca-app-pub-3940256099942544~3347511713";
        public const string iosAdmobSampleAppID = "ca-app-pub-3940256099942544~1458002511";

        public const string editorRuntimeActiveAdPrefs = "typesadsavailable";
        public const string editorReimportDepsOnBuildPrefs = "cas_reimport_deps_on_build";
        public const string editorLatestVersionPrefs = "cas_last_ver_";
        public const string editorLatestVersionTimestampPrefs = "cas_last_ver_time_";
        public const string rootCASFolderPath = "Assets/CleverAdsSolutions";
        public const string editorFolderPath = rootCASFolderPath + "/Editor";
        public const string androidLibFolderPath = "Assets/Plugins/Android/CASPlugin.androidlib";
        public const string androidResSettingsPath = androidLibFolderPath + "/res/raw/cas_settings.json";
        public const string androidLibManifestPath = androidLibFolderPath + "/AndroidManifest.xml";
        public const string androidLibPropertiesPath = androidLibFolderPath + "/project.properties";

        public const string iosResSettingsPath = "Temp/UnityTempFile-cassettings";

        public const string generalTemplateDependency = "CASGeneral";
        public const string teenTemplateDependency = "CASTeen";
        public const string promoTemplateDependency = "CASPromo";
        public const string dependenciesExtension = "Dependencies.xml";

        public const string androidLibManifestTemplateFile = "CASManifest.xml";
        public const string androidLibPropTemplateFile = "CASLibProperties.txt";
        public const string iosSKAdNetworksTemplateFile = "CASSKAdNetworks.txt";

        private const string gitRootURL = "https://github.com/cleveradssolutions/";
        public const string gitUnityRepo = "CAS-Unity";
        public const string gitAndroidRepo = "CAS-Android";
        public const string gitiOSRepo = "CAS-iSO";
        public const string gitUnityRepoURL = gitRootURL + gitUnityRepo;
        public const string supportURL = gitRootURL + gitUnityRepo + "#support";
        public const string websiteURL = "https://cleveradssolutions.com";
        public const string configuringPrivacyURL = gitRootURL + gitiOSRepo + "#step-5-configuring-privacy-controls";

        public const string mainGradlePath = "Assets/Plugins/Android/mainTemplate.gradle";
        public const string launcherGradlePath = "Assets/Plugins/Android/launcherTemplate.gradle";
        public const string projectGradlePath = "Assets/Plugins/Android/baseProjectTemplate.gradle";
        public const string packageManifestPath = "Packages/manifest.json";

        private const string locationUsageDefaultDescription = "Your data will be used to provide you a better and personalized ad experience.";

        public const string preferredCountry = "BR"; // ISO2: US, RU ...
        #endregion

        [Serializable]
        internal class AdmobAppIdData
        {
            public string admob_app_id = null;
        }

        [Serializable]
        internal class GitVersionInfo
        {
            public string tag_name = null;
        }

        #region Menu items
        [MenuItem( "Assets/CleverAdsSolutions/Android Settings..." )]
        public static void OpenAndroidSettingsWindow()
        {
            OpenSettingsWindow( BuildTarget.Android );
        }

        [MenuItem( "Assets/CleverAdsSolutions/iOS Settings..." )]
        public static void OpenIOSSettingsWindow()
        {
            OpenSettingsWindow( BuildTarget.iOS );
        }
        #endregion

        public static bool IsFirebaseServiceExist( string service )
        {
            if (AssetDatabase.FindAssets( "Firebase." + service ).Length > 0)
                return true;

            return IsPackageExist( "com.google.firebase." + service );
        }

        public static bool IsPackageExist( string package )
        {
            return File.Exists( packageManifestPath ) &&
                File.ReadAllText( packageManifestPath ).Contains( package );
        }

        public static void OpenSettingsWindow( BuildTarget target )
        {
            var asset = GetSettingsAsset( target );
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject( asset );
        }

        public static string GetAdmobAppIdFromJson( string json )
        {
            return JsonUtility.FromJson<AdmobAppIdData>( json ).admob_app_id;
        }

        public static CASInitSettings GetSettingsAsset( BuildTarget platform )
        {
            if (!AssetDatabase.IsValidFolder( "Assets/Resources" ))
                AssetDatabase.CreateFolder( "Assets", "Resources" );
            var assetPath = "Assets/Resources/CASSettings" + platform.ToString() + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CASInitSettings>( assetPath );
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<CASInitSettings>();
                if (platform == BuildTarget.Android)
                {
                    asset.managerIds = new string[] {
                        PlayerSettings.GetApplicationIdentifier( BuildTargetGroup.Android )
                    };
                }
                else if (platform == BuildTarget.iOS)
                {
                    asset.managerIds = new string[] { "" };
                    asset.locationUsageDescription = locationUsageDefaultDescription;
                    asset.interstitialInterval = 90;
                }
                AssetDatabase.CreateAsset( asset, assetPath );
            }
            return asset;
        }

        public static void CreateFolderInAssets( string folderName )
        {
            if (!AssetDatabase.IsValidFolder( rootCASFolderPath ))
                AssetDatabase.CreateFolder( "Assets", "CleverAdsSolutions" );
            if (!AssetDatabase.IsValidFolder( rootCASFolderPath + "/" + folderName ))
                AssetDatabase.CreateFolder( rootCASFolderPath, folderName );
        }

        public static bool IsDependencyFileExists( string dependency, BuildTarget platform )
        {
            return AssetDatabase.FindAssets( dependency + platform.ToString() + "Dependencies" ).Length > 0;
        }

        public static string GetTemplatePath( string templateFile )
        {
            string templateFolder = "/Templates/" + templateFile;
            string path = "Packages/" + packageName + templateFolder;
            if (!File.Exists( path ))
            {
                path = rootCASFolderPath + templateFolder;
                if (!File.Exists( path ))
                {
                    Debug.LogError( logTag + "Template " + templateFile + " file not found. Try reimport CAS package." );
                    return null;
                }
            }
            return path;
        }

        public static bool TryActivateDependencies( string template, BuildTarget platform )
        {
            CreateFolderInAssets( "Editor" );

            string fromPath = GetTemplatePath( template + platform.ToString() + ".xml" );
            if (string.IsNullOrEmpty( fromPath ))
                return false;

            string dest = editorFolderPath + "/" + template + platform.ToString() + dependenciesExtension;
            return TryCopyFile( fromPath, dest );
        }

        public static bool TryCopyFile( string source, string dest )
        {
            try
            {
                AssetDatabase.DeleteAsset( dest );
                File.Copy( source, dest );
                AssetDatabase.ImportAsset( dest );
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException( e );
                return false;
            }
        }

        public static void WriteToFile( string data, string path )
        {
            try
            {
                var directoryPath = Path.GetDirectoryName( path );
                if (!Directory.Exists( directoryPath ))
                    Directory.CreateDirectory( directoryPath );
                File.WriteAllText( path, data );
                AssetDatabase.ImportAsset( path );
            }
            catch (Exception e)
            {
                Debug.LogException( e );
            }
        }

        public static bool IsPortraitOrientation()
        {
            var orientation = PlayerSettings.defaultInterfaceOrientation;
            if (orientation == UIOrientation.Portrait || orientation == UIOrientation.PortraitUpsideDown)
            {
                return true;
            }
            else if (orientation == UIOrientation.AutoRotation)
            {
                if (PlayerSettings.allowedAutorotateToPortrait
                    && !PlayerSettings.allowedAutorotateToLandscapeRight
                    && !PlayerSettings.allowedAutorotateToLandscapeLeft)
                    return true;
            }
            return false;
        }

        public static void StopBuildWithMessage( string message, BuildTarget target )
        {
            EditorUtility.ClearProgressBar();
            if (target != BuildTarget.NoTarget
                    && EditorUtility.DisplayDialog( "CAS Stop Build", message, "Open Settings", "Close" ))
                OpenSettingsWindow( target );

#if UNITY_2018_1_OR_NEWER
            throw new BuildFailedException( logTag + message );
#elif UNITY_2017_1_OR_NEWER
            throw new BuildPlayerWindow.BuildMethodException( logTag + message );
#else
            throw new OperationCanceledException(logTag + message);
#endif
        }

        public static string BuildRemoteUrl( string managerID, string country, BuildTarget platform )
        {
            string platformCode;
            switch (platform)
            {
                case BuildTarget.Android:
                    platformCode = "0";
                    break;
                case BuildTarget.iOS:
                    platformCode = "1";
                    break;
                default:
                    platformCode = "9";
                    Debug.LogError( "Not supported platform for CAS " + platform.ToString() );
                    break;
            }

            var result = new StringBuilder( "https://psvpromo.psvgamestudio.com/Scr/cas.php?platform=" )
                .Append( platformCode )
                .Append( "&bundle=" ).Append( UnityWebRequest.EscapeURL( managerID ) )
                .Append( "&hash=" ).Append( Md5Sum( managerID + platformCode ) )
                .Append( "&lang=" ).Append( SystemLanguage.English );

            if (!string.IsNullOrEmpty( country ))
                result.Append( "&country=" ).Append( country );
            return result.ToString();
        }

        private static string Md5Sum( string strToEncrypt )
        {
            UTF8Encoding ue = new UTF8Encoding();
            byte[] bytes = ue.GetBytes( strToEncrypt + "MeDiAtIoNhAsH" );
            System.Security.Cryptography.MD5CryptoServiceProvider md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash( bytes );
            StringBuilder hashString = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
                hashString.Append( Convert.ToString( hashBytes[i], 16 ).PadLeft( 2, '0' ) );
            return hashString.ToString().PadLeft( 32, '0' );
        }

        public static string DownloadRemoteSettings( string managerID, string country, BuildTarget platform )
        {
            const string title = "Update CAS remote settings";
            string url = BuildRemoteUrl( managerID, country, platform );
            string message = null;

            using (var loader = UnityWebRequest.Get( url ))
            {
                loader.SendWebRequest();
                while (!loader.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar( title, managerID,
                        Mathf.Repeat( ( float )EditorApplication.timeSinceStartup, 1.0f ) ))
                    {
                        loader.Dispose();
                        message = "Update CAS Settings canceled";
                        break;
                    }
                }
                EditorUtility.ClearProgressBar();

                if (message == null)
                {
                    if (string.IsNullOrEmpty( loader.error ))
                    {
                        EditorUtility.DisplayProgressBar( title, "Write CAS settings", 0.7f );
                        var content = loader.downloadHandler.text.Trim();
                        if (string.IsNullOrEmpty( content ))
                            StopBuildWithMessage( "Server have no settings for " + managerID +
                                " Please try using a different identifier in the first place or contact support." +
                                " To test build please use Test Ad Mode in settings.", platform );

                        return ApplySettingsContent( content, platform );
                    }
                    else
                    {
                        message = "Server response " + loader.responseCode + ": " + loader.error;
                    }
                }
            }
            if (EditorUtility.DisplayDialog( title, message, "Select settings file", "Cancel Build" ))
            {
                var filePath = EditorUtility.OpenFilePanelWithFilters(
                    "Select CAS Settings file for build", "", new[] { "json" } );
                if (!string.IsNullOrEmpty( filePath ))
                    return ApplySettingsContent( File.ReadAllText( filePath ), platform );
            }
            StopBuildWithMessage( message, BuildTarget.NoTarget );
            return null;
        }

        public static string ApplySettingsContent( string content, BuildTarget target )
        {
            if (target == BuildTarget.Android)
                WriteToFile( content, androidResSettingsPath );
            else
                WriteToFile( content, iosResSettingsPath );
            return GetAdmobAppIdFromJson( content );
        }

        public static string GetNewVersionOrNull( string repo, string currVersion, bool force )
        {
            try
            {
                var newVerStr = GetLatestVersion( repo, force );
                if (newVerStr != null && newVerStr != currVersion)
                {
                    var currVer = new System.Version( currVersion );
                    var newVer = new System.Version( newVerStr );
                    if (currVer < newVer)
                        return newVerStr;
                }
            }
            catch (Exception e)
            {
                Debug.LogException( e );
            }
            return null;
        }

        public static string GetLatestVersion( string repo, bool force )
        {
            if (!force && !HasTimePassed( editorLatestVersionTimestampPrefs + repo, 1, false ))
            {
                var last = PlayerPrefs.GetString( editorLatestVersionPrefs + repo );
                if (!string.IsNullOrEmpty( last ))
                    return last;
            }

            const string title = "Get latest CAS version info";
            string url = "https://api.github.com/repos/cleveradssolutions/" + repo + "/releases/latest";

            using (var loader = UnityWebRequest.Get( url ))
            {
                loader.SendWebRequest();
                try
                {
                    while (!loader.isDone)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar( title, repo,
                            Mathf.Repeat( ( float )EditorApplication.timeSinceStartup, 1.0f ) ))
                        {
                            loader.Dispose();
                            return null;
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (string.IsNullOrEmpty( loader.error ))
                {
                    var content = loader.downloadHandler.text;
                    var versionInfo = JsonUtility.FromJson<GitVersionInfo>( content );
                    if (!string.IsNullOrEmpty( versionInfo.tag_name ))
                    {
                        PlayerPrefs.SetString( editorLatestVersionPrefs + repo, versionInfo.tag_name );
                        EditorPrefs.SetString( editorLatestVersionTimestampPrefs + repo, DateTime.Now.ToBinary().ToString() );
                    }
                    return versionInfo.tag_name;
                }
                else
                {
                    Debug.LogError( logTag + "Response " + loader.responseCode + ": " + loader.error );
                }
            }

            return null;
        }

        public static bool HasTimePassed( string prefKey, int days, bool projectOnly )
        {
            string pref;
            if (projectOnly)
                pref = PlayerPrefs.GetString( prefKey, string.Empty );
            else
                pref = EditorPrefs.GetString( prefKey, string.Empty );

            if (string.IsNullOrEmpty( pref ))
            {
                return true;
            }
            else
            {
                DateTime checkTime;
                try
                {
                    long binartDate = long.Parse( pref );
                    checkTime = DateTime.FromBinary( binartDate );
                }
                catch
                {
                    return true;
                }
                checkTime = checkTime.Add( TimeSpan.FromDays( days ) );
                return DateTime.Compare( DateTime.Now, checkTime ) > 0; // Now time is later than checkTime
            }
        }
    }
}