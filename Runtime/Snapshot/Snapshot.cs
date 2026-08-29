using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    /// <summary>스냅샷 한 줄 — 「어느 엔티티의 어느 컴포넌트가 어떤 값이었나」.</summary>
    public readonly struct SnapshotEntry
    {
        public readonly int EntityID;
        public readonly int Generation;
        public readonly Type ComponentType;

        /// <summary>그 컴포넌트가 낸 값. <see cref="ISnapshotable"/>이 아니면 <c>null</c>이다.</summary>
        public readonly object State;

        public SnapshotEntry(int entityID, int generation, Type componentType, object state)
        {
            EntityID = entityID;
            Generation = generation;
            ComponentType = componentType;
            State = state;
        }

        public override string ToString()
            => $"{EntityID}:{Generation} {ComponentType?.Name} = {State?.ToString() ?? "(값 없음)"}";
    }

    /// <summary>어떤 스텝에서의 세계 하나.</summary>
    /// <remarks>
    /// <b>되돌리지 않는다.</b> 뜨고 비교하는 것까지가 이것이 하는 일이다.
    /// 복원은 엔티티를 되살리는 문제로 이어지고, 그때는 컴포넌트가 든 Unity 오브젝트 참조와
    /// <see cref="Entity"/> 손잡이를 어떻게 되맞출지부터 정해야 한다.
    ///
    /// <b>컴포넌트가 하나도 없는 엔티티는 남지 않는다.</b> 줄은 컴포넌트 단위라서다.
    /// </remarks>
    public sealed class Snapshot
    {
        /// <summary>뜬 시점의 <see cref="Context.Tick"/> — Update 레인의 스텝 번호.</summary>
        public uint Tick { get; }

        /// <summary>뜬 시점의 <see cref="Context.FixedTick"/> — 물리 레인의 스텝 번호.</summary>
        /// <remarks>
        /// <b>두 레인의 번호를 다 담는 이유.</b> 어느 레인에서 떴는지는 뜨는 쪽이 정하고,
        /// 하나만 담으면 다른 레인에서 뜬 스냅샷에 <b>엉뚱한 번호가 붙는다.</b>
        /// 한 프레임에 FixedUpdate가 세 번 돌면 <see cref="Tick"/>은 셋 다 같으므로,
        /// 그 셋을 가르는 것은 이 값뿐이다.
        /// </remarks>
        public uint FixedTick { get; }

        public IReadOnlyList<SnapshotEntry> Entries { get; }

        /// <remarks>
        /// <b>만드는 곳은 <see cref="ContextSnapshotExtensions.Capture"/> 하나다.</b>
        /// 밖에서 지어 낸 스냅샷은 어느 세계도 가리키지 않으므로 <see cref="Diff"/>의 결과가
        /// 무엇을 뜻하는지 말할 수 없다.
        /// </remarks>
        internal Snapshot(uint tick, uint fixedTick, IReadOnlyList<SnapshotEntry> entries)
        {
            Tick = tick;
            FixedTick = fixedTick;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public int Count => Entries.Count;

        public override string ToString()
            => $"Snapshot(Tick={Tick} FixedTick={FixedTick} {Count}줄)";

        /// <summary>두 스냅샷의 차이. 순서는 <paramref name="after"/> 기준이고, 사라진 것이 뒤에 붙는다.</summary>
        /// <remarks>
        /// 키는 <c>(EntityID, Generation, ComponentType)</c>이다. <b>세대가 다르면 다른 엔티티</b>이므로,
        /// 지웠다 같은 ID로 다시 만든 것은 <see cref="ChangeKind.Removed"/>와
        /// <see cref="ChangeKind.Added"/>로 나뉘어 나온다. 그것이 사실이다.
        ///
        /// <see cref="ISnapshotable"/>이 아닌 컴포넌트는 양쪽 값이 <c>null</c>이라
        /// <b>붙고 떨어진 것만</b> 잡히고 값 변화는 잡히지 않는다.
        /// </remarks>
        public static List<Change> Diff(Snapshot before, Snapshot after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var remaining = new Dictionary<Key, object>(before.Entries.Count);
            foreach (var e in before.Entries)
                remaining[new Key(e.EntityID, e.Generation, e.ComponentType)] = e.State;

            var changes = new List<Change>();

            foreach (var e in after.Entries)
            {
                var key = new Key(e.EntityID, e.Generation, e.ComponentType);
                if (!remaining.TryGetValue(key, out var was))
                {
                    changes.Add(new Change(ChangeKind.Added, e.EntityID, e.Generation, e.ComponentType, null, e.State));
                    continue;
                }

                remaining.Remove(key);
                if (!Equals(was, e.State))
                    changes.Add(new Change(ChangeKind.Modified, e.EntityID, e.Generation, e.ComponentType, was, e.State));
            }

            foreach (var pair in remaining)
                changes.Add(new Change(ChangeKind.Removed, pair.Key.ID, pair.Key.Generation, pair.Key.Type, pair.Value, null));

            return changes;
        }

        private readonly struct Key : IEquatable<Key>
        {
            public readonly int ID;
            public readonly int Generation;
            public readonly Type Type;

            public Key(int id, int generation, Type type) { ID = id; Generation = generation; Type = type; }

            public bool Equals(Key other) => ID == other.ID && Generation == other.Generation && Type == other.Type;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(ID, Generation, Type);
        }
    }

    public enum ChangeKind
    {
        /// <summary>뒤 스냅샷에만 있다 — 엔티티가 생겼거나 컴포넌트가 붙었다.</summary>
        Added,

        /// <summary>앞 스냅샷에만 있다 — 엔티티가 죽었거나 컴포넌트가 떨어졌다.</summary>
        Removed,

        /// <summary>양쪽에 있는데 값이 다르다.</summary>
        Modified
    }

    /// <summary>두 스냅샷 사이의 변화 하나.</summary>
    public readonly struct Change
    {
        public readonly ChangeKind Kind;
        public readonly int EntityID;
        public readonly int Generation;
        public readonly Type ComponentType;

        /// <summary><see cref="ChangeKind.Added"/>면 <c>null</c>.</summary>
        public readonly object Before;

        /// <summary><see cref="ChangeKind.Removed"/>면 <c>null</c>.</summary>
        public readonly object After;

        public Change(ChangeKind kind, int entityID, int generation, Type componentType, object before, object after)
        {
            Kind = kind;
            EntityID = entityID;
            Generation = generation;
            ComponentType = componentType;
            Before = before;
            After = after;
        }

        public override string ToString() => Kind switch
        {
            ChangeKind.Added => $"+ {EntityID}:{Generation} {ComponentType?.Name} = {After?.ToString() ?? "(값 없음)"}",
            ChangeKind.Removed => $"- {EntityID}:{Generation} {ComponentType?.Name}",
            _ => $"~ {EntityID}:{Generation} {ComponentType?.Name}: {Before} -> {After}"
        };
    }
}
