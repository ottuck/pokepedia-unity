using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.WebGL;
using UnityEngine;

namespace Pokepedia.EditorTools
{
    /// <summary>
    /// 웹 빌드. Release 는 Disk Size + LTO(작지만 7~10분), Dev 는 빌드 속도 우선(반복 확인용).
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Pokepedia.EditorTools.WebBuild.Release -logFile -
    /// </summary>
    public static class WebBuild
    {
        [MenuItem("Pokepedia/Build Web (Release)")]
        public static void Release() => Build(WasmCodeOptimization.DiskSizeLTO, "Builds/Web");

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
