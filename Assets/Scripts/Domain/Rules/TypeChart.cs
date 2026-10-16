using System;
using System.Collections.Generic;

namespace Pokepedia.Domain.Rules
{
    /// <summary>
    /// 18×18 타입 상성표 (현대 기준, 6세대 이후).
    /// 배수는 2(효과가 굉장했다) / 0.5(효과가 별로인 듯하다) / 0(효과가 없는 것 같다) / 1.
    /// </summary>
    /// <remarks>
    /// 출처 (2026-10-16 확인, 두 출처의 324칸이 모두 일치):
    /// - PokeAPI type 엔드포인트의 damage_relations (현재 세대 값): https://pokeapi.co/api/v2/type/{name}/
    /// - Pokémon Database 타입 상성표 (Gen 6+): https://pokemondb.net/type
    /// Bulbapedia(https://bulbapedia.bulbagarden.net/wiki/Type/Type_chart)는 자동 요청을 403으로 막아 직접 대조하지 못했다.
    /// 1이 아닌 칸은 120개(2배 51, 0.5배 61, 0배 8).
    /// </remarks>
    public static class TypeChart
    {
        const double O = 1.0; // 보통
        const double S = 2.0; // 효과가 굉장했다
        const double H = 0.5; // 효과가 별로인 듯하다
        const double N = 0.0; // 효과가 없는 것 같다

        // 행 = 공격 타입, 열 = 방어 타입. 순서는 PokemonType enum 순서를 따른다.
        static readonly double[,] Chart =
        {
            //            Nor Fir Wat Ele Gra Ice Fig Poi Gro Fly Psy Bug Roc Gho Dra Dar Ste Fai
            /* Normal   */ { O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  H,  N,  O,  O,  H,  O },
            /* Fire     */ { O,  H,  H,  O,  S,  S,  O,  O,  O,  O,  O,  S,  H,  O,  H,  O,  S,  O },
            /* Water    */ { O,  S,  H,  O,  H,  O,  O,  O,  S,  O,  O,  O,  S,  O,  H,  O,  O,  O },
            /* Electric */ { O,  O,  S,  H,  H,  O,  O,  O,  N,  S,  O,  O,  O,  O,  H,  O,  O,  O },
            /* Grass    */ { O,  H,  S,  O,  H,  O,  O,  H,  S,  H,  O,  H,  S,  O,  H,  O,  H,  O },
            /* Ice      */ { O,  H,  H,  O,  S,  H,  O,  O,  S,  S,  O,  O,  O,  O,  S,  O,  H,  O },
            /* Fighting */ { S,  O,  O,  O,  O,  S,  O,  H,  O,  H,  H,  H,  S,  N,  O,  S,  S,  H },
            /* Poison   */ { O,  O,  O,  O,  S,  O,  O,  H,  H,  O,  O,  O,  H,  H,  O,  O,  N,  S },
            /* Ground   */ { O,  S,  O,  S,  H,  O,  O,  S,  O,  N,  O,  H,  S,  O,  O,  O,  S,  O },
            /* Flying   */ { O,  O,  O,  H,  S,  O,  S,  O,  O,  O,  O,  S,  H,  O,  O,  O,  H,  O },
            /* Psychic  */ { O,  O,  O,  O,  O,  O,  S,  S,  O,  O,  H,  O,  O,  O,  O,  N,  H,  O },
            /* Bug      */ { O,  H,  O,  O,  S,  O,  H,  H,  O,  H,  S,  O,  O,  H,  O,  S,  H,  H },
            /* Rock     */ { O,  S,  O,  O,  O,  S,  H,  O,  H,  S,  O,  S,  O,  O,  O,  O,  H,  O },
            /* Ghost    */ { N,  O,  O,  O,  O,  O,  O,  O,  O,  O,  S,  O,  O,  S,  O,  H,  O,  O },
            /* Dragon   */ { O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  O,  S,  O,  H,  N },
            /* Dark     */ { O,  O,  O,  O,  O,  O,  H,  O,  O,  O,  S,  O,  O,  S,  O,  H,  O,  H },
            /* Steel    */ { O,  H,  H,  H,  O,  S,  O,  O,  O,  O,  O,  O,  S,  O,  O,  O,  H,  S },
            /* Fairy    */ { O,  H,  O,  O,  O,  O,  S,  H,  O,  O,  O,  O,  O,  O,  S,  S,  H,  O },
        };

        /// <summary>공격 타입 → 방어 타입 하나에 대한 배수</summary>
        public static double Effectiveness(PokemonType attack, PokemonType defend)
        {
            return Chart[(int)attack, (int)defend];
        }

        /// <summary>이중 타입이면 두 배수를 곱한다 (예: 0.25, 4)</summary>
        /// <exception cref="ArgumentException">defenderTypes 가 null 이거나 1~2개가 아닐 때</exception>
        public static double Effectiveness(PokemonType attack, IReadOnlyList<PokemonType> defenderTypes)
        {
            if (defenderTypes == null) throw new ArgumentNullException(nameof(defenderTypes));
            if (defenderTypes.Count < 1 || defenderTypes.Count > 2)
                throw new ArgumentException($"defender must have 1 or 2 types, got {defenderTypes.Count}", nameof(defenderTypes));

            double multiplier = 1.0;
            for (int i = 0; i < defenderTypes.Count; i++)
                multiplier *= Effectiveness(attack, defenderTypes[i]);
            return multiplier;
        }
    }
}
