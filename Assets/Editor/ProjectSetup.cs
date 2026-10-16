using System.IO;
using Pokepedia.Bridge;
using Pokepedia.Presentation;
using Pokepedia.Presentation.Battle;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pokepedia.EditorTools
{
    /// <summary>
    /// M0 결론을 프로젝트 설정에 반영하고 진입 씬을 만든다. 여러 번 실행해도 같은 결과가 되게 작성한다.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Pokepedia.EditorTools.ProjectSetup.Apply
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScene = "Assets/Scenes/Main.unity";

        [MenuItem("Pokepedia/Setup Project")]
        public static void Apply()
        {
            ApplyPlayerSettings();
            FontAtlasBuilder.Build();
            AudioBaker.Bake();
            ConfigureTmpDefaults();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] done");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "ottuck";
            PlayerSettings.productName = "Pokepedia Battle";

            // 스플래시: 로고 텍스처(2.7MB)는 showUnityLogo 까지 꺼야 빌드에서 빠진다 (UUM-63958)
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
            PlayerSettings.SplashScreen.show = false;

            var web = NamedBuildTarget.WebGL;
            // High 는 리플렉션으로만 쓰이는 코드를 지울 수 있어 되돌렸다(2026-10-17). Minimal 은 클래스 라이브러리·UnityEngine 만 깎는다.
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.template = "PROJECT:Pokepedia";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            // IndexedDB 캐시는 끈다: 동시 탭에서 data 손상 1회 재현, Safari iframe 미지원 → HTTP 캐시(immutable)에 맡긴다
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            // 캔버스가 포커스를 잃어도(iframe 밖 Pokepedia 화면 클릭, 다른 창으로 전환) 멈추거나 느려지지 않게 한다.
            // 탭 자체가 숨겨지면 브라우저가 requestAnimationFrame 을 멈추므로 이 설정으로도 프레임이 돌지 않는다(브라우저 정책).
            PlayerSettings.runInBackground = true;
        }

        static void ConfigureTmpDefaults()
        {
            // TMP 기본 리소스(LiberationSans 1.3MB, EmojiOne 0.3MB)는 Resources 폴더라 무조건 빌드에 들어간다 → Galmuri 로 바꾸고 삭제.
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            var galmuri = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAtlasBuilder.OutPath);
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = galmuri;
            so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = null;
            so.FindProperty("m_fallbackFontAssets").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            foreach (var guid in AssetDatabase.FindAssets("LiberationSans", new[] { "Assets/TextMesh Pro" }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var guid in AssetDatabase.FindAssets("EmojiOne", new[] { "Assets/TextMesh Pro" }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.5f; // 144px / 16PPU / 2
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.22f, 0.06f);
            camGo.transform.position = new Vector3(0, 0, -10);

            var bridge = new GameObject("HostBridge", typeof(HostBridge)).GetComponent<HostBridge>();

            var battle = new GameObject("Battle", typeof(BattleScreen)).GetComponent<BattleScreen>();
            var bso = new SerializedObject(battle);
            AssignCommon(bso);
            bso.FindProperty("speciesJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/species.json");
            bso.FindProperty("movesJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/moves.json");
            bso.FindProperty("rosterJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/roster.json");
            bso.FindProperty("animationJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Data/animation.json");
            bso.FindProperty("flashMaterial").objectReferenceValue = MaterialAsset("Assets/Content/VFX/UIFlash.mat", "Pokepedia/UI Flash");
            // 파티클: URP 2D 렌더러가 그리는 스프라이트 Unlit 셰이더. 텍스처 없이 정사각형 입자(도트 느낌)
            bso.FindProperty("particleMaterial").objectReferenceValue = MaterialAsset("Assets/Content/VFX/Particle.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            bso.ApplyModifiedPropertiesWithoutUndo();

            var app = new GameObject("App", typeof(TitleScreen)).GetComponent<TitleScreen>();
            var so = new SerializedObject(app);
            so.FindProperty("bridge").objectReferenceValue = bridge;
            so.FindProperty("battle").objectReferenceValue = battle;
            AssignCommon(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MainScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }

        /// <summary>
        /// 셰이더를 참조하는 머티리얼을 에셋으로 만든다. 코드에서 Shader.Find 만 쓰면 빌드에서 셰이더가 빠질 수 있어서,
        /// 씬이 참조하는 머티리얼 에셋으로 셰이더를 빌드에 포함시킨다.
        /// </summary>
        static Material MaterialAsset(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("shader not found: " + shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        static void AssignCommon(SerializedObject so)
        {
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAtlasBuilder.OutPath);
            so.FindProperty("localeKo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Locale/ko.json");
            so.FindProperty("localeEn").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Locale/en.json");
            so.FindProperty("localeJa").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Locale/ja.json");
        }
    }
}
