using System.IO;
using Matsuri.Core;
using Matsuri.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// 仕様書 §55 の方針を実行するツール。
    /// シーンの中身を .unity に手で組まず、コードから生成する。
    /// バッチモードからも呼べるよう、すべて static メソッドで公開する。
    /// </summary>
    public static class MatsuriProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/Festival.unity";
        /// <summary>MatsuriCatalog は Resources 配下に置く（実行時に Resources.Load するため）。</summary>
        public const string CatalogPath = "Assets/ScriptableObjects/Resources/MatsuriCatalog.asset";

        /// <summary>置き場所が変わっていてもプロジェクト全体から拾えるようにする。</summary>
        public static MatsuriCatalog FindCatalog()
        {
            var direct = AssetDatabase.LoadAssetAtPath<MatsuriCatalog>(CatalogPath);
            if (direct != null) return direct;

            var guids = AssetDatabase.FindAssets("t:MatsuriCatalog");
            if (guids.Length == 0) return null;

            var found = AssetDatabase.LoadAssetAtPath<MatsuriCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (found == null) return null;

            // Resources の外にあると実行時に読めないので移動しておく。
            var current = AssetDatabase.GetAssetPath(found);
            if (!current.Contains("/Resources/"))
            {
                var dir = Path.GetDirectoryName(CatalogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
                var error = AssetDatabase.MoveAsset(current, CatalogPath);
                if (string.IsNullOrEmpty(error))
                {
                    MatsuriLog.Build($"MatsuriCatalog を Resources へ移動しました: {CatalogPath}");
                    found = AssetDatabase.LoadAssetAtPath<MatsuriCatalog>(CatalogPath);
                }
                else
                {
                    MatsuriLog.Warn($"MatsuriCatalog を Resources へ移動できませんでした: {error}");
                }
            }
            return found;
        }

        [MenuItem("Matsuri/2. Build Scene", priority = 20)]
        public static void BuildScene()
        {
            var catalog = FindCatalog();
            if (catalog == null)
            {
                MatsuriLog.Error(
                    "MatsuriCatalog が見つかりません。先に メニュー Matsuri/1. Generate Data Assets を実行してください。");
                return;
            }

            var dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // シーンに置くのは Bootstrap 1個だけ。世界は実行時にコードが組み立てる。
            var go = new GameObject("MatsuriBootstrap");
            var bootstrap = go.AddComponent<MatsuriBootstrap>();

            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("_catalog");
            if (prop != null)
            {
                prop.objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterSceneInBuildSettings();

            MatsuriLog.Build($"シーンを生成しました: {ScenePath}");
        }

        static void RegisterSceneInBuildSettings()
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
            {
                if (s.path == ScenePath)
                {
                    if (!s.enabled) s.enabled = true;
                    EditorBuildSettings.scenes = existing;
                    return;
                }
            }

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("Matsuri/3. Build All (Data + Scene)", priority = 30)]
        public static void BuildAll()
        {
            DataAssetGenerator.GenerateAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildScene();
            MatsuriLog.Build("Build All 完了");
        }

        [MenuItem("Matsuri/Open Festival Scene", priority = 40)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                BuildScene();
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
