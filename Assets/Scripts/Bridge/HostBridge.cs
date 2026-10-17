using System;
using System.Runtime.InteropServices;
using Pokepedia.App.Host;
using UnityEngine;

namespace Pokepedia.Bridge
{
    /// <summary>
    /// IHostBridge 의 Unity 구현. WebGL 빌드에서는 HostBridge.jslib 를 통해 window.postMessage 를 쓰고,
    /// 에디터·기타 플랫폼에서는 Null 구현처럼 동작한다(로그만 남김).
    /// JS 가 SendMessage("HostBridge", "OnHostInit", json) 으로 호출하므로 GameObject 이름이 "HostBridge" 여야 한다.
    /// </summary>
    public sealed class HostBridge : MonoBehaviour, IHostBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HostBridge_Post(string json);
        [DllImport("__Internal")] static extern IntPtr HostBridge_GetSearch();
#endif

        public event Action<InitMessage> InitReceived;

        public bool IsHostMode { get; private set; }

        /// <summary>주소에 ?lab 이 있으면 연출 실험실(전용 연출 후보를 골라 보는 화면)로 연다</summary>
        public bool IsLabMode { get; private set; }

        /// <summary>주소에 ?fps 가 있으면 프레임 표시를 켠다 (성능 측정용)</summary>
        public bool IsFpsMode { get; private set; }

        /// <summary>주소의 ?lang= (ko/en/ja). Pokepedia 가 페이지 언어를 넘긴다. 없으면 null</summary>
        public string Lang { get; private set; }

        void Awake()
        {
            gameObject.name = "HostBridge";
#if UNITY_WEBGL && !UNITY_EDITOR
            // iframe 안에서 Application.absoluteURL 의 의미가 불명확하므로 location.search 를 직접 읽는다
            var search = Marshal.PtrToStringUTF8(HostBridge_GetSearch());
            IsHostMode = search.Contains("host=pokepedia");
            IsLabMode = search.Contains("lab");
            IsFpsMode = search.Contains("fps");
            int at = search.IndexOf("lang=", StringComparison.Ordinal);
            if (at >= 0 && at + 7 <= search.Length) Lang = search.Substring(at + 5, 2);
#endif
        }

        public void Post(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HostBridge_Post(json);
#else
            Debug.Log("[HostBridge] (null) post " + json);
#endif
        }

        // JS → Unity. 템플릿이 event.origin 을 허용 목록과 대조한 뒤에만 호출한다.
        public void OnHostInit(string json)
        {
            InitMessage msg;
            try { msg = JsonUtility.FromJson<InitMessage>(json); }
            catch (Exception e) { Debug.LogWarning("[HostBridge] bad init: " + e.Message); return; }
            InitReceived?.Invoke(msg);
        }
    }
}
