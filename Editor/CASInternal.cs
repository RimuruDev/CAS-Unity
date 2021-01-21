﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Utils = CAS.UEditor.CASEditorUtils;

namespace CAS.UEditor
{

    internal static class HelpStyles
    {
        private static GUIStyle boxScopeStyle;
        public static GUIStyle wordWrapTextAred;

        public static GUIContent helpIconContent;
        public static GUIContent errorIconContent;
        private static GUIContent tempContent;


        static HelpStyles()
        {
            boxScopeStyle = new GUIStyle( EditorStyles.helpBox );
            boxScopeStyle.padding = new RectOffset( 6, 6, 6, 6 );
            wordWrapTextAred = new GUIStyle( EditorStyles.textArea );
            wordWrapTextAred.wordWrap = true;

            errorIconContent = EditorGUIUtility.IconContent( "d_console.erroricon.sml" );
            helpIconContent = EditorGUIUtility.IconContent( "_Help" );
            tempContent = new GUIContent();
        }

        public static void OnLabelGUI( Dependency.Label label )
        {
            if (label == Dependency.Label.None)
                return;
            if (( label & Dependency.Label.Banner ) == Dependency.Label.Banner)
            {
                tempContent.text = "B";
                tempContent.tooltip = "Support Banner Ad";
                GUILayout.Label( tempContent, "AssetLabel", GUILayout.ExpandWidth( false ) );
            }
            else
            {
                GUILayout.Label( " B ", GUILayout.ExpandWidth( false ) );
            }
            if (( label & Dependency.Label.Inter ) == Dependency.Label.Inter)
            {
                tempContent.text = "I";
                tempContent.tooltip = "Support Interstitial Ad";
                GUILayout.Label( tempContent, "AssetLabel", GUILayout.ExpandWidth( false ) );
            }
            else
            {
                GUILayout.Label( " I ", GUILayout.ExpandWidth( false ) );
            }
            if (( label & Dependency.Label.Reward ) == Dependency.Label.Reward)
            {
                tempContent.text = "R";
                tempContent.tooltip = "Support Rewarded Video Ad";
                GUILayout.Label( tempContent, "AssetLabel", GUILayout.ExpandWidth( false ) );
            }
            else
            {
                GUILayout.Label( " R ", GUILayout.ExpandWidth( false ) );
            }
            if (( label & Dependency.Label.Beta ) == Dependency.Label.Beta)
            {
                tempContent.text = "beta";
                tempContent.tooltip = "Dependencies in closed beta and available upon invite only. " +
                        "If you would like to be considered for the beta, please contact Support.";
                GUILayout.Label( tempContent, "AssetLabel", GUILayout.ExpandWidth( false ) );
            }
        }

        public static void BeginBoxScope()
        {
            EditorGUILayout.BeginVertical( boxScopeStyle );
        }

        public static void EndBoxScope()
        {
            EditorGUILayout.EndVertical();
        }
    }

    public partial class DependencyManager
    {
        #region Internal implementation
        [SerializeField]
        private Dependency[] simple;
        [SerializeField]
        private Dependency[] advanced;
        private List<AdDependency> other = new List<AdDependency>();

        internal GUILayoutOption columnWidth;

        internal bool installedAny;

        private bool firebaseDeepLinksExist;

        private static bool solutionsFoldout = true;
        private static bool advancedFoldout;
        private static bool otherFoldout;

        internal void Init( BuildTarget platform, bool deepInit = true )
        {
            installedAny = false;
            for (int i = 0; i < simple.Length; i++)
                simple[i].Reset();
            for (int i = 0; i < advanced.Length; i++)
                advanced[i].Reset();

            for (int i = 0; i < simple.Length; i++)
                simple[i].Init( this, platform, deepInit );
            for (int i = 0; i < advanced.Length; i++)
                advanced[i].Init( this, platform, deepInit );

            if (!deepInit)
                return;
            other.Clear();
            var depsAssets = AssetDatabase.FindAssets( "Dependencies" );
            for (int i = 0; i < depsAssets.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath( depsAssets[i] );
                if (!path.EndsWith( ".xml" ) || !path.StartsWith( "Assets/" )
                    || path.Contains( "CAS" ) || !path.Contains( "/Editor/" ))
                    continue;
                if (File.Exists( path ))
                {
                    try
                    {
                        var source = File.ReadAllText( path );
                        string begin = platform == BuildTarget.Android ? "<androidPackage spec=\"" : "<iosPod name=\"";
                        var beginIndex = source.IndexOf( begin );
                        while (beginIndex > 0)
                        {
                            beginIndex += begin.Length;
                            other.Add( new AdDependency(
                                source.Substring( beginIndex, source.IndexOf( '\"', beginIndex ) - beginIndex ), path ) );

                            beginIndex = source.IndexOf( begin, beginIndex );
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException( e );
                    }
                }
            }

            firebaseDeepLinksExist = Utils.IsFirebaseServiceExist( "dynamic" );
        }

        private void OnHeaderGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space( 25 );
            GUILayout.Label( "Dependency", EditorStyles.largeLabel );
            GUILayout.Label( "Current", EditorStyles.largeLabel, columnWidth );
            GUILayout.Label( "Latest", EditorStyles.largeLabel, columnWidth );
            GUILayout.Label( "Action", EditorStyles.largeLabel, columnWidth );
            EditorGUILayout.EndHorizontal();
        }

        internal void OnGUI( BuildTarget platform )
        {
            if (!installedAny)
                EditorGUILayout.HelpBox( "Dependencies of native SDK were not found. " +
                    "Please use the following options to integrate solutions or any SDK separately.",
                    MessageType.Warning );

            columnWidth = GUILayout.MaxWidth( EditorGUIUtility.currentViewWidth * 0.15f );

            if (simple.Length > 0)
            {
                HelpStyles.BeginBoxScope();
                solutionsFoldout = GUILayout.Toggle( solutionsFoldout, "Solutions", EditorStyles.foldout );
                if (solutionsFoldout)
                {
                    OnHeaderGUI();
                    for (int i = 0; i < simple.Length; i++)
                        simple[i].OnGUI( this, platform );
                }
                HelpStyles.EndBoxScope();
            }

            if (advanced.Length > 0)
            {
                HelpStyles.BeginBoxScope();
                advancedFoldout = GUILayout.Toggle( advancedFoldout, "Advanced Integration", EditorStyles.foldout );
                if (!advancedFoldout)
                {
                    for (int i = 0; i < advanced.Length; i++)
                    {
                        if (advanced[i].inBan && advanced[i].installedVersion.Length > 0)
                        {
                            advancedFoldout = true;
                            Debug.LogError( Utils.logTag + advanced[i].name +
                                " Dependencies found that are not valid for the applications of the selected audience. " +
                                "Please use 'Assets/CleverAdsSolutions/" + platform.ToString() +
                                " Settings' menu to remove dependencies." );
                            break;
                        }
                    }
                }
                if (advancedFoldout)
                {
                    OnHeaderGUI();
                    for (int i = 0; i < advanced.Length; i++)
                        advanced[i].OnGUI( this, platform );
                }
                HelpStyles.EndBoxScope();
            }
            if (other.Count > 0)
            {
                HelpStyles.BeginBoxScope();
                otherFoldout = GUILayout.Toggle( otherFoldout, "Other Active Dependencies: " + other.Count, EditorStyles.foldout );
                if (otherFoldout)
                {
                    for (int i = 0; i < other.Count; i++)
                        other[i].OnGUI( this );
                }
                HelpStyles.EndBoxScope();
            }

            EditorGUILayout.HelpBox( "Please provide us with a list of integrated dependencies " +
                "so that we can make the correct settings.", MessageType.Info );
            if (platform == BuildTarget.Android)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox( "Changing dependencies will change the project settings. " +
                    "Please use Android Resolver after the change complete.", MessageType.Info );
                if (GUILayout.Button( "Resolve", GUILayout.ExpandWidth( false ), GUILayout.Height( 40 ) ))
                {
#if UNITY_ANDROID
                    var succses = Utils.TryResolveAndroidDependencies();
                    EditorUtility.DisplayDialog( "Android Dependencies",
                        succses ? "Resolution Succeeded" : "Resolution Failed. See the log for details.",
                        "OK" );
#else
                    EditorUtility.DisplayDialog( "Android Dependencies",
                        "Android resolver not enabled. Unity Android platform target must be selected.",
                        "OK" );
#endif
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        internal void SetAudience( Audience audience )
        {
            for (int i = 0; i < simple.Length; i++)
                simple[i].FilterAudience( audience );
            for (int i = 0; i < advanced.Length; i++)
                advanced[i].FilterAudience( audience );
        }

        private struct AdDependency
        {
            public string name;
            public string path;

            public AdDependency( string name, string path )
            {
                this.name = name;
                this.path = path;
            }

            public void OnGUI( DependencyManager mediation )
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label( name );
                //if (GUILayout.Button( "Select", EditorStyles.miniButton, mediation.columnWidth ))
                //{
                //    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( path );
                //    EditorGUIUtility.PingObject( asset );
                //}
                if (GUILayout.Button( "Remove", EditorStyles.miniButton, mediation.columnWidth ))
                {
                    AssetDatabase.MoveAssetToTrash( path );
                    for (int i = mediation.other.Count - 1; i >= 0; i--)
                    {
                        if (mediation.other[i].path == path)
                            mediation.other.RemoveAt( i );
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField( path, EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandHeight( false ) );
                EditorGUI.indentLevel--;
            }
        }
#endregion
    }

    public partial class Dependency
    {
#region Internal implementation
        internal void Reset()
        {
            isNewer = false;
            locked = false;
            isRequired = false;
            installedVersion = "";
        }

        internal void Init( DependencyManager mediation, BuildTarget platform, bool updateContaines )
        {
            var path = Utils.GetDependencyPath( name, platform );
            if (File.Exists( path ))
            {
                var dependency = File.ReadAllText( path );
                const string line = "version=\"";
                var beginIndex = dependency.IndexOf( line, 45 );
                if (beginIndex > 0)
                {
                    beginIndex += line.Length;
                    installedVersion = dependency.Substring( beginIndex, dependency.IndexOf( '\"', beginIndex ) - beginIndex );
                    isNewer = new Version( installedVersion ) < new Version( version );
                    mediation.installedAny = true;
                }

                if (updateContaines)
                {
                    for (int i = 0; i < contains.Length; i++)
                    {
                        var item = mediation.Find( contains[i] );
                        if (item != null)
                            item.locked = true;
                    }

                    if (!string.IsNullOrEmpty( require ))
                    {
                        var item = mediation.Find( require );
                        if (item != null)
                            item.isRequired = true;
                    }
                }
            }
        }

        internal void FilterAudience( Audience audience )
        {
            if (audience == Audience.Mixed)
                inBan = false;
            else if (audience == Audience.Children)
                inBan = filter < 1;
            else
                inBan = filter == -1 || filter > 1;
        }

        internal void OnGUI( DependencyManager mediation, BuildTarget platform )
        {
            EditorGUILayout.BeginHorizontal();
            bool installed = !string.IsNullOrEmpty( installedVersion );
            if (( inBan && installed ) || ( installed && locked ) || ( isRequired && !locked && !installed ))
                GUILayout.Label( HelpStyles.errorIconContent, GUILayout.Width( 20 ) );
            else if (string.IsNullOrEmpty( url ))
                GUILayout.Space( 25 );
            else if (GUILayout.Button( HelpStyles.helpIconContent, EditorStyles.label, GUILayout.Width( 20 ) ))
                Application.OpenURL( url );

            HelpStyles.OnLabelGUI( labels );
            GUILayout.Label( name );

            if (installed)
            {
                if (!isNewer || inBan)
                {
                    GUILayout.Label( "Latest", mediation.columnWidth );
                    GUILayout.Label( version, mediation.columnWidth );
                }
                else
                {
                    GUILayout.Label( installedVersion, mediation.columnWidth );
                    if (GUILayout.Button( version, EditorStyles.miniButton, mediation.columnWidth ))
                        ActivateDependencies( platform, mediation );
                }

                var requiredLabel = isRequired && !locked;
                EditorGUI.BeginDisabledGroup( requiredLabel );
                if (GUILayout.Button( requiredLabel ? "Required" : "Remove", EditorStyles.miniButton, mediation.columnWidth ))
                    DisableDependencies( platform, mediation );
                EditorGUI.EndDisabledGroup();
            }
            else if (locked)
            {
                EditorGUI.BeginDisabledGroup( true );
                GUILayout.Label( "", mediation.columnWidth );
                GUILayout.Label( version, mediation.columnWidth );
                GUILayout.Label( "Required", EditorStyles.miniButton, mediation.columnWidth );
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUI.BeginDisabledGroup( inBan || dependencies.Length == 0 );
                GUILayout.Label( "none", mediation.columnWidth );
                GUILayout.Label( version, mediation.columnWidth );
                if (GUILayout.Button( "Install", EditorStyles.miniButton, mediation.columnWidth ))
                    ActivateDependencies( platform, mediation );
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
            if (contains.Length > 0 && !( contains.Length == 1 && contains[0] == "Base" ))
                EditorGUILayout.HelpBox( string.Join( ", ", contains ), MessageType.None );
        }

        private void DisableDependencies( BuildTarget platform, DependencyManager mediation )
        {
            var destination = Utils.GetDependencyPath( name, platform );
            if (File.Exists( destination ))
            {
                AssetDatabase.DeleteAsset( destination );
                installedVersion = "";
                mediation.Init( platform );
            }
        }

        private void ActivateDependencies( BuildTarget platform, DependencyManager mediation )
        {
            if (dependencies.Length == 0)
            {
                Debug.LogError( Utils.logTag + name + " have no dependencies. Please try reimport CAS package." );
                return;
            }
            if (locked)
            {
                return;
            }

            string dependencyName;
            string sourceName;
            string sourcesName;
            string attrName;
            if (platform == BuildTarget.Android)
            {
                dependencyName = "androidPackage";
                sourcesName = "repositories";
                sourceName = "repository";
                attrName = "spec";
            }
            else
            {
                dependencyName = "iosPod";
                sourcesName = "sources";
                sourceName = "source";
                attrName = "name";
            }

            var destination = Utils.GetDependencyPath( name, platform );
            EditorUtility.DisplayProgressBar( "Create dependency", destination, 0.2f );

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine( "<?xml version=\"1.0\" encoding=\"utf-8\"?>" )
                       .AppendLine( "<dependencies>" )
                       .Append( "  <" ).Append( dependencyName ).Append( "s>" ).AppendLine();

                for (int i = 0; i < dependencies.Length; i++)
                {
                    builder.Append( "    <" ).Append( dependencyName ).Append( ' ' )
                        .Append( attrName ).Append( "=\"" ).Append( dependencies[i] )
                        .Append( "\" version=\"" ).Append( version ).Append( "\">" ).AppendLine();
                    if (i == 0 && ( source.Length > 0 || contains.Length > 0 ))
                    {
                        builder.Append( "      <" ).Append( sourcesName ).Append( '>' ).AppendLine();

                        int containsIndex = 0;
                        Dependency item = this;
                        do
                        {
                            for (int sourceIndex = 0; sourceIndex < item.source.Length; sourceIndex++)
                                builder.Append( "        <" ).Append( sourceName ).Append( '>' )
                                    .Append( item.source[sourceIndex] )
                                    .Append( "</" ).Append( sourceName ).Append( '>' ).AppendLine();

                            item = null;
                            while (containsIndex < contains.Length && item == null)
                                item = mediation.Find( contains[containsIndex++] );

                            if (item != null)
                            {
                                item.locked = true;
                            }
                        } while (item != null);

                        builder.Append( "      </" ).Append( sourcesName ).Append( '>' ).AppendLine();
                    }
                    builder.Append( "    </" ).Append( dependencyName ).Append( '>' ).AppendLine();
                }
                builder.Append( "  </" ).Append( dependencyName ).Append( "s>" ).AppendLine()
                       .AppendLine( "</dependencies>" );


                Utils.CreateFolderInAssets( "Editor" );
                var replace = File.Exists( destination );
                File.WriteAllText( destination, builder.ToString() );
                if (!replace)
                    AssetDatabase.ImportAsset( destination );

                Init( mediation, platform, false );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (!string.IsNullOrEmpty( require ) && !File.Exists( Utils.GetDependencyPath( require, platform ) ))
            {
                var item = mediation.Find( require );
                if (item != null)
                {
                    item.isRequired = true;
                    item.ActivateDependencies( platform, mediation );
                }
            }
        }
#endregion
    }
}
