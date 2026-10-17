using System;
using System.Collections.Generic;
using Pokepedia.Domain;

namespace Pokepedia.Content
{
    /// <summary>
    /// 검증을 마친 콘텐츠 묶음. 생성 시 교차 검증한다:
    /// 기본기 정확히 1개, 타입별 대표 기술·보조 기술 각각 최대 1개, 모든 종의 모든 타입에 대표 기술 존재, 로스터 id 가 종 목록에 존재.
    /// 기술 배정(PROJECT.md 6.3): 기본기 + 속성 기술 2개. 이중 타입은 타입 순서대로 대표 기술 하나씩,
    /// 단일 타입은 그 타입의 대표 기술 + 보조 기술(있으면).
    /// </summary>
    public sealed class ContentCatalog
    {
        readonly Dictionary<int, SpeciesInfo> speciesById = new Dictionary<int, SpeciesInfo>();
        readonly Dictionary<string, MoveInfo> movesById = new Dictionary<string, MoveInfo>();
        readonly Dictionary<PokemonType, MoveInfo> signatureByType = new Dictionary<PokemonType, MoveInfo>();
        readonly Dictionary<PokemonType, MoveInfo> secondaryByType = new Dictionary<PokemonType, MoveInfo>();
        readonly Dictionary<int, IReadOnlyList<MoveInfo>> moveSets = new Dictionary<int, IReadOnlyList<MoveInfo>>();
        readonly Dictionary<int, IReadOnlyList<Move>> domainMoves = new Dictionary<int, IReadOnlyList<Move>>();

        public IReadOnlyList<SpeciesInfo> Species { get; }
        public IReadOnlyList<MoveInfo> Moves { get; }
        public IReadOnlyList<RosterEntry> Roster { get; }
        public MoveInfo BasicMove { get; }

        public ContentCatalog(IReadOnlyList<SpeciesInfo> species, IReadOnlyList<MoveInfo> moves, IReadOnlyList<RosterEntry> roster)
        {
            Species = species ?? throw new ArgumentNullException(nameof(species));
            Moves = moves ?? throw new ArgumentNullException(nameof(moves));
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));

            foreach (var s in species)
            {
                if (speciesById.ContainsKey(s.Id)) throw new FormatException($"species: duplicate id {s.Id}");
                speciesById[s.Id] = s;
            }

            foreach (var m in moves)
            {
                if (movesById.ContainsKey(m.Id)) throw new FormatException($"moves: duplicate id '{m.Id}'");
                movesById[m.Id] = m;
                if (m.Role == MoveRole.Basic)
                {
                    if (BasicMove != null) throw new FormatException($"moves: more than one basic move ('{BasicMove.Id}', '{m.Id}')");
                    BasicMove = m;
                }
                else
                {
                    var byType = m.Role == MoveRole.Signature ? signatureByType : secondaryByType;
                    if (byType.TryGetValue(m.Move.Type, out var other))
                        throw new FormatException(
                            $"moves: type '{ContentLoader.ToJsonName(m.Move.Type)}' has more than one {(m.Role == MoveRole.Signature ? "signature" : "secondary")} move ('{other.Id}', '{m.Id}')");
                    byType[m.Move.Type] = m;
                }
            }
            if (BasicMove == null) throw new FormatException("moves: no basic move (role \"basic\")");

            foreach (var s in species)
            {
                var set = new List<MoveInfo> { BasicMove };
                foreach (var type in s.Species.Types)
                {
                    if (!signatureByType.TryGetValue(type, out var sig))
                        throw new FormatException(
                            $"species id {s.Id}: no signature move for type '{ContentLoader.ToJsonName(type)}' in moves");
                    set.Add(sig);
                }
                if (s.Species.Types.Count == 1 && secondaryByType.TryGetValue(s.Species.Types[0], out var second))
                    set.Add(second);
                moveSets[s.Id] = set.AsReadOnly();
                domainMoves[s.Id] = set.ConvertAll(m => m.Move).AsReadOnly();
            }

            foreach (var r in roster)
                if (!speciesById.ContainsKey(r.Id))
                    throw new FormatException($"roster id {r.Id}: not found in species");
        }

        public SpeciesInfo GetSpecies(int id)
            => speciesById.TryGetValue(id, out var s) ? s : throw new KeyNotFoundException($"species id {id} not found");

        public bool TryGetSpecies(int id, out SpeciesInfo species) => speciesById.TryGetValue(id, out species);

        public MoveInfo GetMove(string id)
            => movesById.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException($"move id '{id}' not found");

        /// <summary>그 타입의 대표 기술. 없으면 null.</summary>
        public MoveInfo SignatureMoveFor(PokemonType type) => signatureByType.TryGetValue(type, out var m) ? m : null;

        /// <summary>자동 배정된 기술(화면 정보 포함). 순서: 기본기, 종의 타입 순서대로 대표 기술, (단일 타입이면) 보조 기술.</summary>
        public IReadOnlyList<MoveInfo> MoveSetFor(int speciesId)
            => moveSets.TryGetValue(speciesId, out var set) ? set : throw new KeyNotFoundException($"species id {speciesId} not found");

        /// <summary>MoveSetFor 의 Domain Move 만. Battler 생성자에 그대로 넘길 수 있다.</summary>
        public IReadOnlyList<Move> MovesFor(int speciesId)
            => domainMoves.TryGetValue(speciesId, out var list) ? list : throw new KeyNotFoundException($"species id {speciesId} not found");
    }
}
