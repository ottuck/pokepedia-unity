using System;

namespace Pokepedia.App.Host
{
    /// <summary>
    /// React(Pokepedia) 와의 통신 창구. Application 은 이 인터페이스만 알고,
    /// 실제 구현(WebGL jslib / 에디터용 Null)은 Bridge 어셈블리가 제공한다. (의존성 역전)
    /// 메시지 규약은 docs/PROJECT.md 9.2 가 단일 출처다.
    /// </summary>
    public interface IHostBridge
    {
        /// <summary>URL 에 ?host=pokepedia 가 있으면 true (Collection 모드 후보)</summary>
        bool IsHostMode { get; }

        /// <summary>Unity → 호스트 메시지 전송 (JSON 문자열)</summary>
        void Post(string json);

        /// <summary>호스트가 보낸 init 메시지(이미 origin 검증됨)</summary>
        event Action<InitMessage> InitReceived;
    }

    /// <summary>React → Unity init 메시지. JsonUtility 로 읽으므로 public 필드를 쓴다.</summary>
    [Serializable]
    public sealed class InitMessage
    {
        public string type;
        public int v;
        public string locale;
        public OwnedSticker[] owned;
    }

    [Serializable]
    public sealed class OwnedSticker
    {
        public int id;
        public bool shiny;
    }

    public static class HostProtocol
    {
        public const int Version = 1;
        public const string ReadyJson = "{\"type\":\"ready\",\"v\":1}";
    }
}
