using System;

namespace Pokepedia.App.Host
{
    public enum LaunchState
    {
        /// <summary>Begin 전 (START 를 누르기 전)</summary>
        Idle,
        /// <summary>?host 파라미터 없음 → 바로 Demo 모드</summary>
        Demo,
        /// <summary>ready 를 보내고 init 을 기다리는 중</summary>
        WaitingInit,
        /// <summary>init 수신 → Collection 모드</summary>
        Connected,
        /// <summary>제한 시간 초과 → [다시 시도] / [데모 체험] 표시. 자동으로 Demo 로 바꾸지 않는다.</summary>
        TimedOut,
    }

    /// <summary>
    /// 모드 판별과 ready → init 왕복 (설계 문서 9.2).
    /// 시간은 호출자가 넘겨 준다(Unity 의 Time 에 의존하지 않아 테스트가 쉽다).
    /// </summary>
    public sealed class HostHandshake
    {
        public const double TimeoutSeconds = 5.0;

        readonly IHostBridge bridge;
        double waitStartedAt;

        public LaunchState State { get; private set; }
        public InitMessage Init { get; private set; }
        public event Action<LaunchState> StateChanged;

        public HostHandshake(IHostBridge bridge)
        {
            this.bridge = bridge;
            bridge.InitReceived += OnInit;
        }

        /// <summary>START 이후 호출. 호스트 모드면 ready 를 보내고 대기, 아니면 즉시 Demo.</summary>
        public void Begin(double now)
        {
            if (!bridge.IsHostMode) { Set(LaunchState.Demo); return; }
            SendReady(now);
        }

        /// <summary>[다시 시도]: ready 를 다시 보낸다.</summary>
        public void Retry(double now)
        {
            if (State == LaunchState.TimedOut) SendReady(now);
        }

        public void Tick(double now)
        {
            if (State == LaunchState.WaitingInit && now - waitStartedAt >= TimeoutSeconds) Set(LaunchState.TimedOut);
        }

        void SendReady(double now)
        {
            waitStartedAt = now;
            Set(LaunchState.WaitingInit);
            bridge.Post(HostProtocol.ReadyJson);
        }

        void OnInit(InitMessage msg)
        {
            // 버전이 다르거나 대기 중이 아니면 무시한다 (Unity 재로딩 시 React 가 init 을 다시 보낸다)
            if (msg == null || msg.v != HostProtocol.Version) return;
            if (State != LaunchState.WaitingInit && State != LaunchState.TimedOut) return;
            Init = msg;
            Set(LaunchState.Connected);
        }

        void Set(LaunchState s)
        {
            State = s;
            StateChanged?.Invoke(s);
        }
    }
}
