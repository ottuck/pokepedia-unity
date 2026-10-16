using System;
using System.Collections.Generic;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// Web Audio <c>AudioParam</c> 의 자동화 타임라인을 오프라인으로 흉내 낸다.
    /// setValueAtTime / linearRampToValueAtTime / exponentialRampToValueAtTime 만 지원하며,
    /// 값 계산 규칙은 Web Audio 명세(이벤트 시각 이전은 기본값, 램프는 직전 이벤트에서 시작, 마지막 이벤트 뒤는 유지)를 따른다.
    /// 시각은 초 단위 절대 시각(렌더 버퍼 시작 = 0)이다.
    /// </summary>
    internal sealed class Automation
    {
        enum Kind { Set, Linear, Exp }

        struct Ev
        {
            public Kind Kind;
            public double Time;
            public double Value;
        }

        readonly List<Ev> events = new List<Ev>();
        readonly double defaultValue;
        int cursor = -1;

        public Automation(double defaultValue) => this.defaultValue = defaultValue;

        /// <summary>자동화 없이 항상 같은 값.</summary>
        public static Automation Constant(double value) => new Automation(value);

        public bool HasEvents => events.Count > 0;

        public Automation Set(double value, double time) => Add(Kind.Set, value, time);
        public Automation LinearTo(double value, double time) => Add(Kind.Linear, value, time);
        public Automation ExpTo(double value, double time) => Add(Kind.Exp, value, time);

        Automation Add(Kind kind, double value, double time)
        {
            // 같은 시각의 이벤트 뒤, 더 늦은 이벤트 앞에 넣는다(명세의 삽입 규칙).
            int i = events.Count;
            while (i > 0 && events[i - 1].Time > time) i--;
            events.Insert(i, new Ev { Kind = kind, Time = time, Value = value });
            cursor = -1;
            return this;
        }

        /// <summary>시각 t(초)의 값. 대개 증가하는 t 로 호출되므로 커서로 선형 탐색을 줄인다.</summary>
        public double ValueAt(double t)
        {
            int n = events.Count;
            if (n == 0) return defaultValue;
            if (cursor >= 0 && events[cursor].Time > t) cursor = -1;
            while (cursor + 1 < n && events[cursor + 1].Time <= t) cursor++;

            if (cursor < 0) return defaultValue; // 첫 이벤트 이전 (첫 이벤트가 램프인 경우는 쓰지 않는다)

            Ev cur = events[cursor];
            if (cursor + 1 < n)
            {
                Ev next = events[cursor + 1];
                double span = next.Time - cur.Time;
                if (next.Kind == Kind.Linear && span > 0)
                {
                    return cur.Value + (next.Value - cur.Value) * ((t - cur.Time) / span);
                }
                if (next.Kind == Kind.Exp && span > 0)
                {
                    // 명세: 시작값이 0 이거나 부호가 다르면 끝 시각까지 시작값을 유지한다.
                    if (cur.Value == 0 || cur.Value * next.Value <= 0) return cur.Value;
                    return cur.Value * Math.Pow(next.Value / cur.Value, (t - cur.Time) / span);
                }
            }
            return cur.Value;
        }
    }
}
