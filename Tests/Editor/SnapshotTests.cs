using System.Linq;
using NUnit.Framework;
using OVFL.ECS;

namespace OVFL.ECS.Test
{
    /// <summary>
    /// 스냅샷 명세 — 무엇이 남고, 무엇이 「바뀌었다」로 잡히는가.
    /// </summary>
    [TestFixture]
    public class SnapshotTests
    {
        class Score : IComponent, ISnapshotable
        {
            public int Value;
            public object Capture() => new State(Value);

            public readonly struct State
            {
                public readonly int Value;
                public State(int value) => Value = value;
                public override string ToString() => Value.ToString();
            }
        }

        /// <summary>값을 낼 줄 모르는 컴포넌트. 있고 없음만 남는다.</summary>
        class Opaque : IComponent { public int Value; }

        /// <summary>필드가 없는 태그.</summary>
        class Marker : IComponent { }

        // ── Capture ───────────────────────────────────────────────────────

        [Test]
        public void 살아있는_엔티티의_모든_컴포넌트가_한_줄씩_남는다()
        {
            var context = new Context();
            var entity = context.CreateEntity();
            entity.AddComponent(new Score { Value = 3 });
            entity.AddComponent(new Marker());
            context.Flush();

            var snapshot = context.Capture();

            Assert.AreEqual(2, snapshot.Count);
            Assert.IsTrue(snapshot.Entries.Any(e => e.ComponentType == typeof(Score)));
            Assert.IsTrue(snapshot.Entries.Any(e => e.ComponentType == typeof(Marker)));
        }

        [Test]
        public void ISnapshotable이_아니면_값은_null이고_줄은_남는다()
        {
            var context = new Context();
            context.CreateEntity().AddComponent(new Opaque { Value = 7 });
            context.Flush();

            var entry = context.Capture().Entries.Single();

            Assert.AreEqual(typeof(Opaque), entry.ComponentType);
            Assert.IsNull(entry.State, "값을 낼 줄 모르면 담기지 않는다");
        }

        [Test]
        public void 두_레인의_스텝_번호가_따로_남는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            systems.Tick();
            systems.Tick();
            systems.FixedTick();
            systems.FixedTick();
            systems.FixedTick();

            var snapshot = context.Capture();

            Assert.AreEqual(2u, snapshot.Tick);
            Assert.AreEqual(3u, snapshot.FixedTick);
        }

        [Test]
        public void 한_프레임의_여러_FixedTick은_FixedTick으로만_갈린다()
        {
            // Tick은 그 프레임 내내 같은 값이라 물리 스텝을 가르지 못한다.
            var context = new Context();
            var systems = new Systems(context);
            systems.Tick();

            systems.FixedTick();
            var first = context.Capture();
            systems.FixedTick();
            var second = context.Capture();

            Assert.AreEqual(first.Tick, second.Tick, "같은 프레임이라 Update 레인 번호는 같다");
            Assert.AreEqual(1u, first.FixedTick);
            Assert.AreEqual(2u, second.FixedTick);
        }

        [Test]
        public void 아직_Flush되지_않은_엔티티는_빠진다()
        {
            var context = new Context();
            context.CreateEntity().AddComponent(new Score());

            Assert.AreEqual(0, context.Capture().Count, "등장 전에는 세계에 없다");

            context.Flush();
            Assert.AreEqual(1, context.Capture().Count);
        }

        [Test]
        public void 삭제_예약된_엔티티는_빠진다()
        {
            var context = new Context();
            var entity = context.CreateEntity();
            entity.AddComponent(new Score());
            context.Flush();

            context.DestroyEntity(entity);

            Assert.AreEqual(0, context.Capture().Count, "쿼리에서 즉시 사라지므로 스냅샷에도 없다");
        }

        [Test]
        public void 뜬_값은_그_뒤의_변경에_흔들리지_않는다()
        {
            var context = new Context();
            var score = context.CreateEntity().AddComponent(new Score { Value = 1 });
            context.Flush();

            var before = context.Capture();
            score.Value = 99;

            Assert.AreEqual(new Score.State(1), before.Entries.Single().State);
        }

        // ── Diff ──────────────────────────────────────────────────────────

        [Test]
        public void 값이_바뀌면_Modified다()
        {
            var context = new Context();
            var score = context.CreateEntity().AddComponent(new Score { Value = 1 });
            context.Flush();

            var before = context.Capture();
            score.Value = 2;
            var after = context.Capture();

            var change = Snapshot.Diff(before, after).Single();
            Assert.AreEqual(ChangeKind.Modified, change.Kind);
            Assert.AreEqual(typeof(Score), change.ComponentType);
            Assert.AreEqual(new Score.State(1), change.Before);
            Assert.AreEqual(new Score.State(2), change.After);
        }

        [Test]
        public void 값이_그대로면_아무것도_안_나온다()
        {
            var context = new Context();
            context.CreateEntity().AddComponent(new Score { Value = 1 });
            context.Flush();

            Assert.IsEmpty(Snapshot.Diff(context.Capture(), context.Capture()));
        }

        [Test]
        public void 컴포넌트가_붙으면_Added_떨어지면_Removed다()
        {
            var context = new Context();
            var entity = context.CreateEntity();
            entity.AddComponent(new Score());
            context.Flush();

            var before = context.Capture();
            entity.AddComponent(new Marker());
            var added = Snapshot.Diff(before, context.Capture()).Single();

            Assert.AreEqual(ChangeKind.Added, added.Kind);
            Assert.AreEqual(typeof(Marker), added.ComponentType);

            var mid = context.Capture();
            entity.RemoveComponent<Marker>();
            var removed = Snapshot.Diff(mid, context.Capture()).Single();

            Assert.AreEqual(ChangeKind.Removed, removed.Kind);
            Assert.AreEqual(typeof(Marker), removed.ComponentType);
        }

        [Test]
        public void 값을_못_내는_컴포넌트는_붙고_떨어진_것만_잡힌다()
        {
            var context = new Context();
            var opaque = context.CreateEntity().AddComponent(new Opaque { Value = 1 });
            context.Flush();

            var before = context.Capture();
            opaque.Value = 2;

            Assert.IsEmpty(Snapshot.Diff(before, context.Capture()), "값 변화는 볼 수 없다");
        }

        [Test]
        public void 엔티티가_생기면_그_컴포넌트가_Added로_나온다()
        {
            var context = new Context();
            var before = context.Capture();

            context.CreateEntity().AddComponent(new Score { Value = 5 });
            context.Flush();

            var change = Snapshot.Diff(before, context.Capture()).Single();
            Assert.AreEqual(ChangeKind.Added, change.Kind);
            Assert.AreEqual(new Score.State(5), change.After);
        }

        [Test]
        public void 같은_ID로_다시_만들면_Removed와_Added로_갈린다()
        {
            // 세대가 다르면 다른 엔티티다. 「값이 바뀐 하나」로 뭉치면 사실이 아니다.
            var context = new Context();
            var first = context.CreateEntity();
            first.AddComponent(new Score { Value = 1 });
            context.Flush();

            var before = context.Capture();

            context.DestroyEntity(first);
            context.Flush();
            var second = context.CreateEntity();
            second.AddComponent(new Score { Value = 2 });
            context.Flush();

            Assert.AreEqual(first.ID, second.ID, "ID는 재사용된다");
            Assert.AreNotEqual(first.Generation, second.Generation);

            var changes = Snapshot.Diff(before, context.Capture());
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(1, changes.Count(c => c.Kind == ChangeKind.Removed && c.Generation == first.Generation));
            Assert.AreEqual(1, changes.Count(c => c.Kind == ChangeKind.Added && c.Generation == second.Generation));
        }

        [Test]
        public void 엔티티가_죽으면_그_컴포넌트가_Removed로_나온다()
        {
            var context = new Context();
            var entity = context.CreateEntity();
            entity.AddComponent(new Score { Value = 1 });
            entity.AddComponent(new Marker());
            context.Flush();

            var before = context.Capture();
            context.DestroyEntity(entity);
            context.Flush();

            var changes = Snapshot.Diff(before, context.Capture());
            Assert.AreEqual(2, changes.Count);
            Assert.IsTrue(changes.All(c => c.Kind == ChangeKind.Removed));
        }

        [Test]
        public void Diff는_null을_받으면_던진다()
        {
            var snapshot = new Context().Capture();
            Assert.Throws<System.ArgumentNullException>(() => Snapshot.Diff(null, snapshot));
            Assert.Throws<System.ArgumentNullException>(() => Snapshot.Diff(snapshot, null));
        }

        [Test]
        public void Capture는_null_Context에서_던진다()
        {
            Context context = null;
            Assert.Throws<System.ArgumentNullException>(() => context.Capture());
        }

        // ── 스텝과 함께 ───────────────────────────────────────────────────

        [Test]
        public void 한_스텝_동안_무엇이_바뀌었는지_볼_수_있다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var score = context.CreateEntity().AddComponent(new Score { Value = 0 });
            context.Flush();

            systems.Add(Phase.Simulation, new Bump(score));

            var before = context.Capture();
            systems.Tick();
            var after = context.Capture();

            var change = Snapshot.Diff(before, after).Single();
            Assert.AreEqual(ChangeKind.Modified, change.Kind);
            Assert.AreEqual(0u, before.Tick);
            Assert.AreEqual(1u, after.Tick);
            Assert.AreEqual(0u, after.FixedTick, "물리 레인은 돌지 않았다");
        }

        class Bump : ITickSystem
        {
            private readonly Score _score;
            public Context Context { get; set; }
            public Bump(Score score) => _score = score;
            public void Tick() => _score.Value++;
        }
    }
}
