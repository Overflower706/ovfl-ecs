using System;
using System.Collections.Generic;
using NUnit.Framework;
using OVFL.ECS;

namespace OVFL.ECS.Test
{
    /// <summary>
    /// 핵심 명세 — 실행 순서를 무엇이 정하는가, 밖에서 온 변경이 언제 적용되는가.
    /// </summary>
    [TestFixture]
    public class PhaseAndInboxTests
    {
        class Marker : IComponent { }
        class Score : IComponent { public int Value; }

        class Recorder : ITickSystem
        {
            public Context Context { get; set; }
            private readonly string _name;
            private readonly List<string> _sink;
            public Recorder(string name, List<string> sink) { _name = name; _sink = sink; }
            public void Tick() => _sink.Add(_name);
        }

        class Probe : ITickSystem
        {
            public Context Context { get; set; }
            private readonly Action<Context> _body;
            public Probe(Action<Context> body) => _body = body;
            public void Tick() => _body(Context);
        }

        private bool _savedRethrow;

        [SetUp]
        public void SetUp()
        {
            _savedRethrow = Systems.RethrowOnSystemException;
            Systems.RethrowOnSystemException = true;
        }

        [TearDown]
        public void TearDown() => Systems.RethrowOnSystemException = _savedRethrow;

        // ── Phase 순서 ────────────────────────────────────────────────────

        [Test]
        public void 등록_줄_순서를_섞어도_Phase_순서로_실행된다()
        {
            var systems = new Systems(new Context());
            var order = new List<string>();

            systems.Add(Phase.Outbox, new Recorder("Outbox", order));
            systems.Add(Phase.View, new Recorder("View", order));
            systems.Add(Phase.Inbox, new Recorder("Inbox", order));
            systems.Add(Phase.Reaction, new Recorder("Reaction", order));
            systems.Add(Phase.Input, new Recorder("Input", order));
            systems.Add(Phase.Simulation, new Recorder("Simulation", order));

            systems.Tick();

            Assert.AreEqual("Inbox>Input>Simulation>Reaction>View>Outbox", string.Join(">", order));
        }

        [Test]
        public void 같은_Phase_안에서는_등록_순서다()
        {
            // Phase는 큰 덩어리의 순서를 정하고, 그 안은 적은 순서대로 돈다.
            var systems = new Systems(new Context());
            var order = new List<string>();

            systems.Add(Phase.Simulation, new Recorder("first", order));
            systems.Add(Phase.Simulation, new Recorder("second", order));

            systems.Tick();

            Assert.AreEqual("first>second", string.Join(">", order));
        }

        [Test]
        public void 제네릭_Add도_파생_클래스의_재정의를_탄다()
        {
            var systems = new CountingSystems(new Context());

            systems.Add<Recorderless>(Phase.Simulation);

            Assert.AreEqual(1, systems.AddCalls);
        }

        class Recorderless : ITickSystem
        {
            public Context Context { get; set; }
            public void Tick() { }
        }

        class CountingSystems : Systems
        {
            public int AddCalls;
            public CountingSystems(Context c) : base(c) { }
            public override Systems Add(Phase phase, ISystem system)
            {
                AddCalls++;
                return base.Add(phase, system);
            }
        }

        // ── 엔티티 집합은 경계에서만 바뀐다 ───────────────────────────────

        [Test]
        public void 만든_엔티티는_다음_Phase부터_쿼리에_잡힌다()
        {
            var context = new Context();
            var systems = new Systems(context);
            int seenInSamePhase = -1, seenInNextPhase = -1;

            systems.Add(Phase.Input, new Probe(ctx =>
            {
                ctx.CreateEntity().AddComponent<Marker>();
                seenInSamePhase = ctx.GetEntitiesWith<Marker>().Count;
            }));
            systems.Add(Phase.Simulation, new Probe(ctx =>
                seenInNextPhase = ctx.GetEntitiesWith<Marker>().Count));

            systems.Tick();

            Assert.AreEqual(0, seenInSamePhase, "만든 Phase 안에서는 아직 안 보인다");
            Assert.AreEqual(1, seenInNextPhase, "다음 Phase에서 보인다");
        }

        [Test]
        public void 만든_엔티티도_바로_쓸_수는_있다()
        {
            // 미뤄지는 것은 「쿼리에 잡히는 시점」뿐이다. 존재 자체는 즉시다.
            var context = new Context();
            var entity = context.CreateEntity();
            entity.AddComponent(new Score { Value = 7 });

            Assert.IsTrue(context.IsAlive(entity));
            Assert.AreEqual(7, entity.GetComponent<Score>().Value);
            Assert.AreSame(entity, context.GetEntity(entity.ID));
            Assert.AreEqual(1, context.PendingCount);
            Assert.AreEqual(0, context.EntityCount);
        }

        [Test]
        public void 쿼리를_돌면서_만들어도_터지지_않는다()
        {
            var context = new Context();
            for (int i = 0; i < 3; i++) context.CreateEntity().AddComponent<Marker>();
            context.Flush();

            Assert.DoesNotThrow(() =>
            {
                foreach (var _ in context.AllEntities)
                    context.CreateEntity().AddComponent<Marker>();
            });

            Assert.AreEqual(3, context.EntityCount, "열거 중에는 집합이 고정이다");
            context.Flush();
            Assert.AreEqual(6, context.EntityCount);
        }

        [Test]
        public void 등장_전에_지우면_등장하지_않는다()
        {
            var context = new Context();
            var entity = context.CreateEntity();

            Assert.IsTrue(context.DestroyEntity(entity));
            context.Flush();

            Assert.AreEqual(0, context.EntityCount);
            Assert.IsFalse(context.IsAlive(entity));
        }

        // ── 인박스 ────────────────────────────────────────────────────────

        [Test]
        public void 인박스는_스텝의_맨_앞에서_배출된다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var scoreEntity = context.CreateEntity();
            var score = scoreEntity.AddComponent(new Score());
            context.Flush();

            uint appliedAtTick = 0;
            context.Enqueue(ctx =>
            {
                ctx.GetUniqueComponent<Score>().Value = 99;
                appliedAtTick = ctx.Tick;
            });

            Assert.AreEqual(0, score.Value, "넣은 직후에는 반영되지 않는다");
            Assert.AreEqual(1, context.InboxCount);

            systems.Tick();

            Assert.AreEqual(99, score.Value);
            Assert.AreEqual(1u, appliedAtTick, "1번 스텝의 맨 앞에서 적용됐다");
            Assert.AreEqual(0, context.InboxCount);
        }

        [Test]
        public void Inbox_Phase의_시스템은_배출된_결과를_본다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var score = context.CreateEntity().AddComponent(new Score());
            context.Flush();

            context.Enqueue(ctx => ctx.GetUniqueComponent<Score>().Value = 5);

            int seen = -1;
            systems.Add(Phase.Inbox, new Probe(ctx => seen = ctx.GetUniqueComponent<Score>().Value));

            systems.Tick();

            Assert.AreEqual(5, seen);
        }

        [Test]
        public void 시스템이_도는_도중에는_적용되지_않는다()
        {
            // 인박스가 보장하는 것은 「언제」가 아니라 「어디서」다.
            // 한 스텝의 모든 시스템은 같은 세계를 본다.
            var context = new Context();
            var systems = new Systems(context);
            var score = context.CreateEntity().AddComponent(new Score());
            context.Flush();

            var seen = new List<int>();
            foreach (var phase in new[] { Phase.Inbox, Phase.Input, Phase.Simulation,
                                          Phase.Reaction, Phase.View, Phase.Outbox })
            {
                systems.Add(phase, new Probe(ctx =>
                {
                    seen.Add(ctx.GetUniqueComponent<Score>().Value);
                    // 도는 도중에 밖에서 들어온 것처럼 넣어 본다.
                    ctx.Enqueue(c => c.GetUniqueComponent<Score>().Value = 42);
                }));
            }

            systems.Tick();

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 0 }, seen,
                "한 스텝 안에서는 여섯 Phase가 모두 같은 값을 본다");
            Assert.AreEqual(0, score.Value, "이번 스텝에는 하나도 적용되지 않았다");

            systems.Tick();
            Assert.AreEqual(42, score.Value, "다음 배출에서 적용된다");
        }

        [Test]
        public void 배출_도중에_들어온_것은_그_다음_배출로_넘어간다()
        {
            // 매 프레임 도착하는 RPC가 스텝을 영영 끝내지 못하게 만들면 안 된다.
            var context = new Context();
            var systems = new Systems(context);
            int applied = 0;

            void Recurse(Context ctx)
            {
                applied++;
                if (applied < 5) ctx.Enqueue(Recurse);
            }
            context.Enqueue(Recurse);

            systems.Tick();

            Assert.AreEqual(1, applied, "이번 스텝에서는 하나만");
            Assert.AreEqual(1, context.InboxCount, "다음 것은 다음 스텝으로");
        }

        [Test]
        public void 인박스에서_발행한_이벤트는_그_스텝에서_읽힌다()
        {
            // 다리가 RPC를 이벤트로 바꿔 넣는 경로다. 경계가
            // 「배출 → 발행 → 반영」 순서라 Phase.Inbox의 시스템부터 볼 수 있다.
            var context = new Context();
            var systems = new Systems(context);
            context.Enqueue(ctx => ctx.RaiseEvent(new Pinged()));

            int seenAtInbox = 0, seenAtReaction = 0;
            systems.Add(Phase.Inbox, new Probe(ctx =>
                ctx.ProcessEvents<Pinged>((_, __) => seenAtInbox++)));
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<Pinged>((_, __) => seenAtReaction++)));

            systems.Tick();

            Assert.AreEqual(1, seenAtInbox, "배출된 그 경계에서 이미 발행된다");
            Assert.AreEqual(1, seenAtReaction);
        }

        class Pinged : EventComponent { }

        [Test]
        public void 인박스는_배출_시점의_세계에서_실행된다()
        {
            // 다리가 값을 받아 Enqueue한 뒤, 배출 전에 그 엔티티가 사라질 수 있다.
            // 넣을 때가 아니라 꺼낼 때의 세계라서, 넣은 쪽이 IsAlive를 확인해야 한다.
            var context = new Context();
            var systems = new Systems(context);
            var entity = context.CreateEntity();
            entity.AddComponent(new Score());
            context.Flush();

            bool aliveAtDrain = true;
            context.Enqueue(ctx => aliveAtDrain = ctx.IsAlive(entity));
            context.DestroyEntity(entity);

            systems.Tick();

            Assert.IsFalse(aliveAtDrain, "넣을 때는 살아 있었지만 꺼낼 때는 아니다");
        }

        [Test]
        public void null을_넣으면_던진다()
        {
            var context = new Context();

            Assert.Throws<ArgumentNullException>(() => context.Enqueue(null));
        }

        [Test]
        public void 인박스는_FixedTick이_배출하지_않는다()
        {
            // 인박스를 Tick 레인이 소유한다. 두 레인이 나눠 배출하면 인박스 안에서
            // RaiseEvent로 낸 것이 어느 큐로 갈지가 RPC 도착 시점에 따라 갈린다.
            var context = new Context();
            var systems = new Systems(context);
            var score = context.CreateEntity().AddComponent(new Score());
            context.Flush();

            context.Enqueue(ctx => ctx.GetUniqueComponent<Score>().Value = 7);

            systems.FixedTick();
            Assert.AreEqual(0, score.Value, "FixedTick은 배출하지 않는다");
            Assert.AreEqual(1, context.InboxCount);

            systems.Tick();
            Assert.AreEqual(7, score.Value, "Tick이 배출한다");
            Assert.AreEqual(0, context.InboxCount);
        }

        [Test]
        public void FixedTick도_Phase_순서로_돈다()
        {
            // 인박스만 Tick 레인의 것이고, 경계의 나머지는 두 레인이 같다.
            var context = new Context();
            var systems = new Systems(context);
            var order = new List<string>();

            systems.Add(Phase.Outbox, new FixedRecorder("Outbox", order));
            systems.Add(Phase.View, new FixedRecorder("View", order));
            systems.Add(Phase.Inbox, new FixedRecorder("Inbox", order));
            systems.Add(Phase.Simulation, new FixedRecorder("Simulation", order));

            systems.FixedTick();

            Assert.AreEqual("Inbox>Simulation>View>Outbox", string.Join(">", order));
        }

        class FixedRecorder : IFixedTickSystem
        {
            private readonly string name;
            private readonly List<string> log;
            public Context Context { get; set; }
            public FixedRecorder(string name, List<string> log) { this.name = name; this.log = log; }
            public void FixedTick() => log.Add(name);
        }

        [Test]
        public void FixedTick의_이벤트는_fixed_큐로만_간다()
        {
            var context = new Context();
            var systems = new Systems(context);

            int seenFixed = 0, seenTick = 0;
            systems.Add(Phase.Inbox, new FixedProbe(ctx => ctx.RaiseFixedEvent(new Pinged())));
            systems.Add(Phase.Reaction, new FixedProbe(ctx =>
                ctx.ProcessEvents<Pinged>((_, __) => seenFixed++)));
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<Pinged>((_, __) => seenTick++)));

            systems.FixedTick();
            Assert.AreEqual(1, seenFixed, "같은 FixedTick 안의 뒤 Phase가 본다");

            systems.Tick();
            Assert.AreEqual(0, seenTick, "FixedTick이 끝날 때 정리되므로 Tick은 못 본다");
        }

        class FixedProbe : IFixedTickSystem
        {
            private readonly Action<Context> body;
            public Context Context { get; set; }
            public FixedProbe(Action<Context> body) { this.body = body; }
            public void FixedTick() => body(Context);
        }

        // ── 스텝 카운터 ───────────────────────────────────────────────────

        [Test]
        public void Tick과_FixedTick은_따로_센다()
        {
            var context = new Context();
            var systems = new Systems(context);

            systems.Tick();
            systems.Tick();
            systems.FixedTick();

            Assert.AreEqual(2u, context.Tick);
            Assert.AreEqual(1u, context.FixedTick);
        }
    }
}
