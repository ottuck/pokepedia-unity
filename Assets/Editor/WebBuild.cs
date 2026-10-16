using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.WebGL;
using UnityEngine;

namespace Pokepedia.EditorTools
{
    /// <summary>
    /// 웹 빌드. Release 는 Runtime Speed(LTO 없음), Dev 는 빌드 속도 우선(반복 확인용). Disk Size + LTO 는 빌드가 7~10분 걸리고 문제 가능성이 있어 쓰지 않는다.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Pokepedia.EditorTools.WebBuild.Release -logFile -
    /// </summary>
    public static class WebBuild
    {
        [MenuItem("Pokepedia/Build Web (Release)")]
        public static void Release() => Build(WasmCodeOptimization.RuntimeSpeed, "Builds/Web");

        [MenuItem("Pokepedia/Build Web (Dev)")]
        public static void Dev() => Build(WasmCodeOptimization.BuildTimes, "Builds/WebDev");

        static void Build(WasmCodeOptimization opt, string outDir)
        {
            UserBuildSettings.codeOptimization = opt;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.MainScene },
                locationPathName = Environment.GetEnvironmentVariable("POKE_BUILD_OUT") ?? outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log($"[WebBuild] {opt}: {report.summary.result} in {report.summary.totalTime}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
