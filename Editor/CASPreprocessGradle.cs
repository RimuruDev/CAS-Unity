﻿//
//  Clever Ads Solutions Unity Plugin
//
//  Copyright © 2021 CleverAdsSolutions. All rights reserved.
//

#if UNITY_ANDROID || CASDeveloper

//#define ExcludeAndroidxAnnotations
//#define DeclareJavaVersion

// Many SDKs use the new <queries> element for Android 11 in their bundled Android Manifest files.
// The Android Gradle plugin version should support new elements, else this will cause build errors:
// Android resource linking failed
// error: unexpected element <queries> found in <manifest>.
#define UpdateGradleToSupportAndroid11

// Known issue with jCenter repository where repository is not responding
// and gradle build stops with timeout error.
#define ReplaceJCenterToMavenCentral

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using UnityEditor;
using UnityEngine;
using Utils = CAS.UEditor.CASEditorUtils;

namespace CAS.UEditor
{
    internal static class CASPreprocessGradle
    {
        internal static void Configure( CASEditorSettings settings )
        {
            bool baseGradleChanged = false;
            List<string> baseGradle = null;
            List<string> launcherGradle = null;

#if UNITY_2019_3_OR_NEWER
            const string baseGradlePath = Utils.projectGradlePath;
            const string launcherGradlePath = Utils.launcherGradlePath;

            baseGradle = ReadGradleFile( "Base Gradle", baseGradlePath );
            
            if (settings.multiDexEnabled)
                launcherGradle = ReadGradleFile( "Launcher Gradle", launcherGradlePath );
#else
            const string baseGradlePath = Utils.mainGradlePath;
            const string launcherGradlePath = Utils.mainGradlePath;

            baseGradle = ReadGradleFile( "Gradle", baseGradlePath );
            launcherGradle = baseGradle;
#endif

            if (settings.updateGradlePluginVersion
                && UpdateGradlePluginVersion( baseGradle, baseGradlePath ))
                baseGradleChanged = true;

#if ReplaceJCenterToMavenCentral
            if (UpdateBaseGradleRepositories( baseGradle, baseGradlePath ))
                baseGradleChanged = true;
#endif

            // Enabled by default Dexing artifact transform causes issues for ExoPlayer with Gradle plugin 3.5.0+
            var dexingArtifactProp = new GradleProperty(
                "android.enableDexingArtifactTransform", "false" );

            GradleProperty[] gradleProps = null;
            if (Utils.GetAndroidResolverSetting<bool>( "UseJetifier" ))
            {
                gradleProps = new[] { dexingArtifactProp };
            }
            else
            {
                gradleProps = new[]
                {
                    dexingArtifactProp,
                    new GradleProperty("android.useAndroidX", "true"),
                    new GradleProperty("android.enableJetifier", "true")
                };
            }

#if UNITY_2019_3_OR_NEWER
            List<string> propsFile = ReadGradleFile( "Gradle Properties", Utils.propertiesGradlePath );

            if (UpdateGradlePropertiesFile( propsFile, gradleProps ))
                File.WriteAllLines( Utils.propertiesGradlePath, propsFile.ToArray() );
#else
            // Unity below version 2019.3 does not have a Gradle Properties file
            // and changes are applied to the base Gradle file.
            if (UpdateGradlePropertiesInMainFile( baseGradle, gradleProps, baseGradlePath ))
                baseGradleChanged = true;

            
            if (FixGradleCompatibilityUnity2018( baseGradle, baseGradlePath ))
                baseGradleChanged = true;
#endif

            if (launcherGradle != null)
            {
                if (UpdateLauncherGradleFile( launcherGradle, settings, launcherGradlePath ))
                {
#if UNITY_2019_3_OR_NEWER
                    File.WriteAllLines( launcherGradlePath, launcherGradle.ToArray() );
                    AssetDatabase.ImportAsset( launcherGradlePath );
#else
                    // Unity below version 2019.3 does not have a Gradle Launcher file
                    // and changes are applied to the base Gradle file.
                    baseGradleChanged = true;
#endif
                }
            }

            if (baseGradleChanged)
            {
                File.WriteAllLines( baseGradlePath, baseGradle.ToArray() );
                AssetDatabase.ImportAsset( baseGradlePath );
            }
        }

        internal static bool TryEnableGradleTemplate( string assetPath )
        {
            var fileName = Path.GetFileName( assetPath );
            var gradleFileFromUnity =
                Path.Combine( Path.Combine( GetAndroidToolsPath(), "GradleTemplates" ), fileName );
            if (!File.Exists( gradleFileFromUnity ))
            {
                Debug.LogError( Utils.logTag + "Template file not found: " + gradleFileFromUnity );
                return false;
            }
            try
            {
                File.Copy( gradleFileFromUnity, Path.GetFullPath( assetPath ), true );
                AssetDatabase.ImportAsset( assetPath );
                Debug.Log( Utils.logTag + "Gradle template activated: " + assetPath );
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException( e );
            }
            return false;
        }

        internal static Version GetGradleWrapperVersion()
        {
            string gradleLibPath;
            if (IsUsedGradleWrapperEmbeddedInUnity())
                gradleLibPath = Path.Combine( Path.Combine( GetAndroidToolsPath(), "gradle" ), "lib" );
            else
                gradleLibPath = Path.Combine( EditorPrefs.GetString( "GradlePath" ), "lib" );

            if (!Directory.Exists( gradleLibPath ))
                return null;

            const string wrapperName = "gradle-wrapper-";
            const string wrapeprExtension = ".jar";
            foreach (var file in Directory.GetFiles( gradleLibPath, wrapperName + "*" + wrapeprExtension ))
            {
                try
                {
                    var prefixLength = Path.Combine( gradleLibPath, wrapperName ).Length;
                    var version = file.Substring( prefixLength, file.Length - prefixLength - wrapeprExtension.Length );
                    if (version.Length > 0)
                        return new Version( version );
                }
                catch (Exception e)
                {
                    Debug.LogException( e );
                }
            }
            return null;
        }

        private static List<string> ReadGradleFile( string prefix, string path )
        {
            try
            {
                if (File.Exists( path ))
                    return new List<string>( File.ReadAllLines( path ) );
            }
            catch (Exception e)
            {
                Debug.LogException( e );
            }

            var message = "A successful build requires do modifications to " + prefix + " template. " +
                "But the template is not activated now.";
            Utils.DialogOrCancelBuild( message + "\nClick Сontinue to activate.", BuildTarget.NoTarget );
            try
            {
                if (TryEnableGradleTemplate( path ) && File.Exists( path ))
                    return new List<string>( File.ReadAllLines( path ) );
            }
            catch (Exception e)
            {
                Debug.LogException( e );
            }
            Utils.StopBuildWithMessage( message, BuildTarget.NoTarget );
            return null;
        }

        private static bool UpdateGradlePropertiesFile( List<string> propFile, GradleProperty[] props )
        {
            var isChanged = false;
            var line = 0;
            while (line < propFile.Count)
            {
                var isRemoved = false;
                for (int i = 0; i < props.Length; i++)
                {
                    if (propFile[line].Contains( props[i].name ))
                    {
                        if (props[i].remove)
                        {
                            isRemoved = true;
                            propFile.RemoveAt( line );
                            Debug.Log( Utils.logTag + "Remove gradle property: " + props[i].name );
                            isChanged = true;
                        }
                        else
                        {
                            props[i].exist = true;
                        }
                        break;
                    }
                }
                if (isRemoved)
                    continue;
                if (propFile[line].Contains( "**ADDITIONAL_PROPERTIES**" ))
                    break;
                ++line;
            }
            for (int i = 0; i < props.Length; i++)
            {
                if (!props[i].exist)
                {
                    propFile.Insert( line, props[i].name + "=" + props[i].enabled );
                    Debug.Log( Utils.logTag + "Set gradle property: " + props[i].name + " = " + props[i].enabled );
                    isChanged = true;
                }
            }
            return isChanged;
        }

        private static bool UpdateGradlePropertiesInMainFile( List<string> gradle, GradleProperty[] props, string filePath )
        {
            const string addBeforeLine = "apply plugin";
            const string beginPropsComment = "// CAS Properties Start";
            const string endPropsComment = "// CAS Properties End";
            var beginPropsLine = -1;
            var isChanged = false;
            var tryFindProp = false;

            var line = -1;
            do
            {
                ++line;
                if (line >= gradle.Count)
                {
                    LogWhenGradleLineNotFound( addBeforeLine, filePath );
                    return isChanged;
                }
                if (gradle[line].Contains( "ext {" ))
                {
                    tryFindProp = true;
                    continue;
                }
                if (gradle[line].StartsWith( beginPropsComment ))
                {
                    beginPropsLine = line;
                    continue;
                }
                if (gradle[line].StartsWith( endPropsComment ))
                {
                    if (beginPropsLine > 0)
                    {
                        var allExist = true;
                        for (int i = 0; i < props.Length; i++)
                        {
                            allExist = allExist && ( props[i].exist || props[i].existByCAS );
                        }
                        if (allExist)
                            return isChanged;
                        var removeCount = line + 1 - beginPropsLine;
                        gradle.RemoveRange( beginPropsLine, removeCount );
                        line -= removeCount;
                        beginPropsLine = -1;
                    }
                    else
                    {
                        gradle.RemoveAt( line );
                        --line;
                    }
                    isChanged = true;
                    continue;
                }
                if (!tryFindProp)
                    continue;
                if (gradle[line].Contains( '}' ))
                {
                    tryFindProp = false;
                    continue;
                }

                for (int i = 0; i < props.Length; i++)
                {
                    if (!gradle[line].Contains( props[i].name ))
                        continue;

                    if (props[i].remove)
                    {
                        gradle.RemoveAt( line );
                        Debug.Log( Utils.logTag + "Remove gradle property: " + props[i].name );
                        isChanged = true;
                        --line;
                        break;
                    }
                    if (beginPropsLine < 0)
                        props[i].existByCAS = true;
                    else
                        props[i].exist = true;
                    break;
                }
            } while (!gradle[line].Contains( addBeforeLine ));

            var writeToFile = false;
            var propertiesLines = new List<string>( 8 + props.Length );
            propertiesLines.Add( beginPropsComment );
            propertiesLines.Add( "([rootProject] + (rootProject.subprojects as List)).each {" );
            propertiesLines.Add( "    ext {" );
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].exist)
                    continue;
                propertiesLines.Add( "        it.setProperty(\"" + props[i].name + "\", " + props[i].enabled + ")" );
                Debug.Log( Utils.logTag + "Set gradle property: " + props[i].name + " = " + props[i].enabled );
                writeToFile = true;
            }
            propertiesLines.Add( "    }" );
            propertiesLines.Add( "}" );
            propertiesLines.Add( endPropsComment );

            if (writeToFile)
                gradle.InsertRange( line, propertiesLines.ToArray() );
            return isChanged || writeToFile;
        }

        private static bool UpdateLauncherGradleFile( List<string> gradle, CASEditorSettings settings, string filePath )
        {
            bool isChanged = false;
            int line = 0;
            bool required = settings.multiDexEnabled;
            // Find dependencies{} scope
            do
            {
                ++line;
                if (line >= gradle.Count)
                {
                    if (required)
                        LogWhenGradleLineNotFound( "dependencies{} scope", filePath );
                    return isChanged;
                }
            } while (!gradle[line].Contains( "implementation" ));

            // Find Multidex dependency in scope
            const string depPrefix = "    implementation '";
            bool multidexExist = false;
            const string multidexAndroidSupport = "com.android.support:multidex:";
            const string multidexAndroidX = "androidx.multidex:multidex:";
            const string miltidexAndroidXLine = depPrefix + multidexAndroidX + "2.0.1' // Added by CAS settings";

            //bool exoPlayerExist = false;
            const string exoPlayerDep = "com.google.android.exoplayer:exoplayer:";
            //const string exoPlayerLine = depPrefix + exoPlayerDep + "2.13.3' // Added by CAS settings";
            do
            {
                ++line;
                if (line >= gradle.Count)
                {
                    if (required)
                        LogWhenGradleLineNotFound( "dependencies{} scope", filePath );
                    return isChanged;
                }
                var removeLine = false;
                if (gradle[line].Contains( multidexAndroidSupport ))
                {
                    removeLine = multidexExist || !settings.multiDexEnabled;
                    if (!removeLine)
                    {
                        gradle[line] = miltidexAndroidXLine;
                        Debug.Log( Utils.logTag + "Updated " + multidexAndroidSupport +
                            " to " + multidexAndroidX + " in " + filePath + Utils.logAutoFeature );
                        isChanged = true;
                    }
                    multidexExist = true;
                }
                else if (gradle[line].Contains( multidexAndroidX ))
                {
                    removeLine = multidexExist || !settings.multiDexEnabled;
                    multidexExist = true;
                }
                else if (gradle[line].Contains( exoPlayerDep ))
                {
                    removeLine = true;
                }
                if (removeLine)
                {
                    Debug.Log( Utils.logTag + "Removed: '" + gradle[line] + "' from: " + filePath );
                    gradle.RemoveAt( line );
                    --line;
                    isChanged = true;
                }
            } while (!gradle[line].Contains( '}' ));

            if (!multidexExist && settings.multiDexEnabled)
            {
                gradle.Insert( line, miltidexAndroidXLine );
                Debug.Log( Utils.logTag + "Appended " + multidexAndroidX + " to " + filePath + Utils.logAutoFeature );
                multidexExist = true;
                isChanged = true;
                ++line;
            }

#if false
            if (!exoPlayerExist && settings.exoPlayerIncluded)
            {
                gradle.Insert( line, exoPlayerLine );
                Debug.Log( Utils.logTag + "Appended " + exoPlayerDep + " to " + filePath + Utils.logAutoFeature );
                exoPlayerExist = true;
                isChanged = true;
                ++line;
            }
#endif

#if DeclareJavaVersion
            const string javaVersion = "JavaVersion.VERSION_1_8";
            var existJavaDeclaration = false;
#endif

#if ExcludeAndroidxAnnotations
            const string excludeOption = "exclude 'META-INF/proguard/androidx-annotations.pro'";
            var packagingOptExist = false;
#endif

            required = settings.multiDexEnabled;
            do // while defaultConfig scope
            {
                ++line;
                if (line >= gradle.Count)
                {
                    if (required)
                        LogWhenGradleLineNotFound( "defaultConfig{} scope", filePath );
                    return isChanged;
                }
#if DeclareJavaVersion
                if (!existJavaDeclaration && gradle[line].Contains( javaVersion ))
                    existJavaDeclaration = true;
#endif
#if ExcludeAndroidxAnnotations
                if (!packagingOptExist && gradle[line].Contains( excludeOption ))
                    packagingOptExist = true;
#endif
            } while (!gradle[line].Contains( "defaultConfig" ));

#if DeclareJavaVersion
            if (!existJavaDeclaration)
            {
                var compileOptions = new[] {
                    "	compileOptions {",
                    "        sourceCompatibility " + javaVersion,
                    "        targetCompatibility " + javaVersion,
                    "	}",
                    ""
                };
                gradle.InsertRange( line, compileOptions );
                line += compileOptions.Length;
                isChanged = true;
                Debug.Log( Utils.logTag + "Appended Compile options to use Java Version 1.8 in " + filePath + Utils.logAutoFeature );
            }
#endif

#if ExcludeAndroidxAnnotations
            if (!packagingOptExist)
            {
                var packagingOptions = new[] {
                    "	packagingOptions {",
                    "        " + excludeOption,
                    "	}",
                    ""
                };
                gradle.InsertRange( line, packagingOptions );
                line += packagingOptions.Length;
                isChanged = true;
                Debug.Log( Utils.logTag + "Appended Packaging options to exclude duplicate androidx-annotations. " + filePath + Utils.logAutoFeature );
            }
#endif

            // Find multidexEnable in defaultConfig{} scope
            const string multidexConfig = "multiDexEnabled";
            if (multidexExist)
            {
                var firstLineInDefaultConfigScope = line + 1;
                multidexExist = false;
                while (line < gradle.Count && !gradle[line].Contains( "buildTypes" ))
                {
                    if (gradle[line].Contains( multidexConfig ))
                    {
                        if (!required)
                        {
                            gradle.RemoveAt( line );
                            isChanged = true;
                        }
                        multidexExist = true;
                        break;
                    }
                    line++;
                }

                if (!multidexExist && required)
                {
                    gradle.Insert( firstLineInDefaultConfigScope,
                        "        " + multidexConfig + " true // Enabled by CAS settings" );
                    Debug.Log( Utils.logTag + "Enable Multidex in Default Config of " + filePath + Utils.logAutoFeature );
                    isChanged = true;
                }
            }
            return isChanged;
        }

        private static bool UpdateGradlePluginVersion( List<string> gradle, string filePath )
        {
            const string gradlePluginVersion = "classpath 'com.android.tools.build:gradle:";
            // Find Gradle Plugin Version
            int lineIndex = 0;
            var beginIndex = -1;
            do
            {
                ++lineIndex;
                if (lineIndex >= gradle.Count)
                {
                    LogWhenGradleLineNotFound( "com.android.tools.build:gradle", filePath );
                    return false;
                }
                beginIndex = gradle[lineIndex].IndexOf( gradlePluginVersion );
            } while (beginIndex < 0);

            try
            {
                beginIndex += gradlePluginVersion.Length;
                var currVerStr = gradle[lineIndex].Substring( beginIndex,
                    gradle[lineIndex].IndexOf( '\'', beginIndex ) - beginIndex );

                // https://developer.android.com/studio/releases/gradle-plugin#updating-gradle
                Version wrapper = GetGradleWrapperVersion();
                Version version = new Version( currVerStr );
                Version target = null;
                if (wrapper != null)
                {
                    if (wrapper.Major == 5)
                    {
                        if (wrapper.Minor < 4)
                            target = new Version( 3, 4, 3 );
                        else if (wrapper.Minor < 6)
                            target = new Version( 3, 5, 4 );
                        else
                            target = new Version( 3, 6, 4 );
                    }
                    else if (wrapper.Major == 6)
                    {
                        if (wrapper.Minor < 5)
                            target = new Version( 4, 0, 2 );

                        // Unity 2018.4 require applying gradle properties with the following construct
                        // ([rootProject] + (rootProject.subprojects as List)).each {
                        //   ext { it.setProperty( "android.useAndroidX", true ) }
                        // }
                        // But gradle 4.2.2 no longer support such methods
                        // So, we want to use maximum 4.1.3 version.
#if false
                        else if (wrapper.Minor < 7)
                            target = new Version( 4, 1, 3 );
                        else
                            target = new Version( 4, 2, 2 );
#else
                        else
                            target = new Version( 4, 1, 3 );
#endif
                    }
                }
                else if (version.Major == 4)
                {
                    if (version.Minor == 0 && version.Build < 2)
                        target = new Version( 4, 0, 2 );
                }
                else if (version.Major == 3)
                {
                    switch (version.Minor)
                    {
                        case 3:
                        case 4:
                            target = new Version( 3, version.Minor, 3 );
                            break;
                        case 5:
                        case 6:
                            target = new Version( 3, version.Minor, 4 );
                            break;
                    }
                }

                if (target == null)
                    return false;

                if (version.Major != target.Major
                    || version.Minor != target.Minor
                    || version.Build < target.Build)
                {
                    var oldLine = gradle[lineIndex];
                    gradle[lineIndex] = gradle[lineIndex].Replace( currVerStr, target.ToString() );
                    Debug.Log( Utils.logTag + "Updated Gradle Build Tools Plugin version.\n" +
                                "From: " + oldLine + "\nTo:" + gradle[lineIndex] + Utils.logAutoFeature );
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException( e );
            }
            return false;
        }

        private static bool UpdateBaseGradleRepositories( List<string> gradle, string filePath )
        {
            // Find allprojects { repositories { } }
            int line = 0;
            do
            {
                ++line;
                if (gradle.Count - 1 < line)
                {
                    LogWhenGradleLineNotFound( "repositories{}", filePath );
                    return false;
                }
            } while (!gradle[line].Contains( "repositories" ));

            ++line; // Move in repositories
            // Add MavenCentral repo and remove deprecated jcenter repo
            const string mavenCentralLine = "mavenCentral()";
            var mavenCentralExist = false;
            var beginReposLine = line;
            var isChanged = false;
            while (line < gradle.Count)
            {
                if (gradle[line].Contains( "jcenter()" ))
                {
                    gradle.RemoveAt( line );
                    Debug.Log( Utils.logTag + "Deprecated jCenter repository removed from " + filePath );
                    isChanged = true;
                }
                else if (gradle[line].Contains( mavenCentralLine ))
                {
                    mavenCentralExist = true;
                }
                else if (gradle[line].Contains( '}' ))
                {
                    if (!mavenCentralExist)
                    {
                        gradle.Insert( beginReposLine, "        " + mavenCentralLine );
                        Debug.Log( Utils.logTag + "Maven Central repository appended to " + filePath );
                        isChanged = true;
                    }
                    break;
                }
                ++line;
            }
            return isChanged;
        }

        private static bool FixGradleCompatibilityUnity2018( List<string> gradle, string filePath )
        {
            // New Gradle Wrapper 3.6+ generates a `gradleOut-release.aab`,
            // but Unity 2018 look for a `gradleOut.aab` instead.
            // So we create new taskto rename AAB file for Unity build system.

            // Emdedded version does not require fix
            var requireFix = !IsUsedGradleWrapperEmbeddedInUnity();

            const string message = "Fix Gradle version compatibility by CAS";
            const string beginContentLine = "// " + message + " Start";
            const string endContentLine = "// " + message + " End";
            const string findLine = "**BUILT_APK_LOCATION**";
            var line = gradle.Count;
            var endContantLine = -1;
            do
            {
                line--;
                if (line < 0)
                {
                    LogWhenGradleLineNotFound( findLine, filePath );
                    return false;
                }
                if (endContantLine < 0)
                {
                    if (gradle[line].Contains( endContentLine ))
                        endContantLine = line;
                    continue;
                }
                if (gradle[line].Contains( beginContentLine ))
                {
                    if (!requireFix)
                    {
                        gradle.RemoveRange( line, endContantLine - line + 1 );
                        return true;
                    }
                    return false;
                }
            } while (!gradle[line].Contains( findLine ));

            if (!requireFix)
                return false;

            string[] content = {
                beginContentLine,
                "tasks.whenTaskAdded { task ->",
                "    if (task.name.startsWith(\"bundle\")) {",
                "        def flavor = task.name.substring(\"bundle\".length()).uncapitalize()",
                "        def newTask = tasks.create(\"fixName\" + task.name.capitalize(), Copy) {",
                "            from(\"$buildDir/outputs/bundle/$flavor/\")",
                "            include \"gradleOut-\" + flavor + \".aab\"",
                "            destinationDir file(\"$buildDir/outputs/bundle/$flavor/\")",
                "            rename \"gradleOut-\" + flavor + \".aab\", \"gradleOut.aab\"",
                "        }",
                "        task.finalizedBy(newTask.name)",
                "    }",
                "}",
                endContentLine
            };
            gradle.AddRange( content );
            Debug.Log( Utils.logTag + message + " in " + filePath );
            return true;
        }

        private static void LogWhenGradleLineNotFound( string line, string inFile )
        {
            Debug.LogWarning( Utils.logTag + "Not found " + line + " in Gradle template.\n" +
                            "Please try to remove `" + inFile + "` and enable gradle template in Player Settings.\n" );
        }

        private static bool IsUsedGradleWrapperEmbeddedInUnity()
        {
            return EditorPrefs.GetBool( "GradleUseEmbedded" );
        }

        private static string GetAndroidToolsPath()
        {
            // App path ends `version/Unity.app` or `version/Editor/Unity.exe`
            var appPath = EditorApplication.applicationPath;
            var result = Path.GetDirectoryName( EditorApplication.applicationPath );
            // Windows path: 2020.3.11\Editor\Data\PlaybackEngines\AndroidPlayer\Tools\gradle\lib
            // Macos path: 2020.3.11/PlaybackEngines/AndroidPlayer/Tools/gradle/lib
            if (appPath.EndsWith( ".exe" ))
                result = Path.Combine( result, "Data" );
            return Path.Combine( Path.Combine( Path.Combine( result, "PlaybackEngines" ), "AndroidPlayer" ), "Tools" );
        }


        private class GradleProperty
        {
            public readonly string name;
            public readonly string enabled;
            public bool exist = false;
            public bool remove = false;
            public bool existByCAS = false;

            public GradleProperty( string name, string enabled )
            {
                this.name = name;
                this.enabled = enabled;
            }

            public GradleProperty( string name, string enabled, bool remove )
            {
                this.enabled = enabled;
                this.name = name;
                this.remove = remove;
                exist = remove;
            }
        }
    }
}
#endif