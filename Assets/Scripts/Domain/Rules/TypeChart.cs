using System;
using System.Collections.Generic;

namespace Pokepedia.Domain.Rules
{
    /// <summary>
    /// 18×18 타입 상성표 (현대 기준, 6세대 이후).
    /// 배수는 2(효과가 굉장했다) / 0.5(효과가 별로인 듯하다) / 0(효과가 없는 것 같다) / 1.
    /// </summary>
    public static class TypeChart
    {
        /// <summary>공격 타입 → 방어 타입 하나에 대한 배수</summary>
        public static double Effectiveness(PokemonType attack, PokemonType defend)
        {
            throw new NotImplementedException();
        }

        /// <summary>이중 타입이면 두 배수를 곱한다 (예: 0.25, 4)</summary>
        public static double Effectiveness(PokemonType attack, IReadOnlyList<PokemonType> defenderTypes)
        {
            throw new NotImplementedException();
        }
    }
}
