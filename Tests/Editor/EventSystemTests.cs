using System;
using System.Collections.Generic;
using NUnit.Framework;
using OVFL.ECS;

namespace OVFL.ECS.Test
{
    /// <summary>
    /// 이벤트의 수명 명세. 3.0.0에서 발행·정리가 <see cref="Systems"/> 안으로 들어왔으므로
    /// 더 이상 EventPublisherSystem / EventCleanupSystem을 등록하지 않는다.
    /// </summary>
    [TestFixture]
    public class EventSystemTests
    {
        class DamageEvent : EventComponent { public int Amount; }
        class HealEvent : EventComponent { public int Amount; }

        class Raiser : ITickSystem
        {
            public Context Context { get; set; }
            public void Tick() => Context.RaiseEvent(new DamageEvent { Amount = 10 });
        }

        class FixedRaiser : IFixedTickSystem
        {
            public Context Context { get; set; }
            public void FixedTick() => Context.RaiseFixedEvent(new DamageEvent { Amount = 10 });
        }

        class Reader : ITickSystem
        {
            public Context Context { get; set; }
            public int Seen;
            public uint LastTick;
            public void Tick() => Context.ProcessEvents<DamageEvent>((_, __) =>
            {
                Seen++;
                LastTick = Context.Tick;
            });
        }

        class FixedReader : IFixedTickSystem
        {
            public Context Context { get; set; }
            public int Seen;
            public void FixedTick() => Context.ProcessEvents<DamageEvent>((_, __) => Seen++);
        }

        /// <summary>Context를 받아 임의의 일을 하는 시스템. 테스트용.</summary>
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

        // ── 언제 보이나 ───────────────────────────────────────────────────

        [Test]
        public void 뒤_Phase에서_읽힌다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var reader = new Reader();
            systems.Add(Phase.Input, new Raiser());
            systems.Add(Phase.Reaction, reader);

            systems.Tick();

            Assert.AreEqual(1, reader.Seen);
            Assert.AreEqual(1u, reader.LastTick, "발행한 그 스텝에서 읽힌다");
        }

        [Test]
        public void 같은_Phase에서는_보이지_않는다()
        {
            // 이것이 Phase 경계 규칙의 값이다 — 같은 Phase 안의 등록 순서가
            // 이벤트를 통해서는 결과를 바꾸지 못한다.
            var context = new Context();
            var systems = new Systems(context);
            var reader = new Reader();
            systems.Add(Phase.Input, new Raiser());
            systems.Add(Phase.Input, reader);

            systems.Tick();

            Assert.AreEqual(0, reader.Seen);
        }

        [Test]
        public void 발행만_하고_Tick을_안_돌면_엔티티가_생기지_않는다()
        {
            var context = new Context();
            context.RaiseEvent(new DamageEvent { Amount = 10 });

            Assert.AreEqual(1, context.PendingEventCount);
            Assert.AreEqual(0, context.EntityCount);
        }

        [Test]
        public void 이벤트는_스텝_끝에_사라진다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var reader = new Reader();
            systems.Add(Phase.Input, new Raiser());
            systems.Add(Phase.Reaction, reader);

            systems.Tick();
            Assert.AreEqual(0, context.EntityCount, "스텝이 끝나면 정리된다");

            systems.Tick();
            Assert.AreEqual(2, reader.Seen, "다음 스텝에도 정확히 하나씩");
        }

        [Test]
        public void 여러_시스템이_같은_이벤트를_읽는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var a = new Reader();
            var b = new Reader();
            systems.Add(Phase.Input, new Raiser());
            systems.Add(Phase.Simulation, a);
            systems.Add(Phase.Reaction, b);

            systems.Tick();

            Assert.AreEqual(1, a.Seen);
            Assert.AreEqual(1, b.Seen);
        }

        // ── Fixed 레인 ────────────────────────────────────────────────────

        [Test]
        public void FixedEvent는_FixedTick에서_돌고_그_스텝_끝에_정리된다()
        {
            var context = new Context();
            var systems = new Systems(context);
            var reader = new FixedReader();
            systems.Add(Phase.Input, new FixedRaiser());
            systems.Add(Phase.Reaction, reader);

            systems.FixedTick();

            Assert.AreEqual(1, reader.Seen);
            Assert.AreEqual(0, context.EntityCount, "FixedTick 끝에 정리된다");
        }

        [Test]
        public void Tick은_Fixed_이벤트를_발행하지_않는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            context.RaiseFixedEvent(new DamageEvent { Amount = 1 });

            systems.Tick();

            Assert.AreEqual(0, context.EntityCount, "Tick은 Fixed 큐를 건드리지 않는다");
        }

        [Test]
        public void 두_레인은_서로의_이벤트를_지우지_않는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            int normalSeen = 0;
            systems.Add(Phase.Input, new FixedRaiser());
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<DamageEvent>((_, __) => normalSeen++)));

            systems.FixedTick();  // Fixed 이벤트가 발행되고 그 스텝 끝에 정리된다
            systems.Tick();       // 여기서는 볼 것이 없어야 한다

            Assert.AreEqual(0, normalSeen);
        }

        // ── 메타데이터 ────────────────────────────────────────────────────

        [Test]
        public void 메타데이터에_발행_스텝과_타입이_남는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            EventMetadataComponent meta = null;
            systems.Add(Phase.Input, new Raiser());
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<DamageEvent>((entity, _) =>
                    meta = entity.GetComponent<EventMetadataComponent>())));

            systems.Tick();
            systems.Tick();

            Assert.IsNotNull(meta);
            Assert.AreEqual(2u, meta.CreatedTick, "두 번째 스텝에 발행됐다");
            Assert.AreEqual(nameof(DamageEvent), meta.EventTypeName);
            Assert.IsFalse(meta.IsFixed);
        }

        // ── ProcessEvents ─────────────────────────────────────────────────

        [Test]
        public void 중첩해서_불러도_바깥쪽_열거가_망가지지_않는다()
        {
            // 스냅샷 버퍼를 정적 하나로 두면 안쪽 호출이 바깥쪽 목록을 덮어써서
            // 바깥쪽이 이벤트를 흘린다. 풀이어야 하는 이유다.
            var context = new Context();
            var systems = new Systems(context);
            context.RaiseEvent(new DamageEvent { Amount = 1 });
            context.RaiseEvent(new DamageEvent { Amount = 2 });

            int outer = 0, inner = 0;
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<DamageEvent>((_, __) =>
                {
                    outer++;
                    ctx.ProcessEvents<DamageEvent>((_2, __2) => inner++);
                })));

            systems.Tick();

            Assert.AreEqual(2, outer, "바깥쪽이 두 개를 다 본다");
            Assert.AreEqual(4, inner, "안쪽도 매번 두 개를 다 본다");
        }

        [Test]
        public void 처리_중에_엔티티를_지워도_터지지_않는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            context.RaiseEvent(new DamageEvent { Amount = 1 });
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<DamageEvent>((entity, _) => ctx.DestroyEntity(entity))));

            Assert.DoesNotThrow(() => systems.Tick());
        }

        [Test]
        public void 다른_타입의_이벤트는_읽히지_않는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            context.RaiseEvent(new HealEvent { Amount = 5 });

            int seen = 0;
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEvents<DamageEvent>((_, __) => seen++)));

            systems.Tick();

            Assert.AreEqual(0, seen);
        }

        [Test]
        public void ProcessEventsWhere는_조건에_맞는_것만_읽는다()
        {
            var context = new Context();
            var systems = new Systems(context);
            context.RaiseEvent(new DamageEvent { Amount = 1 });
            context.RaiseEvent(new DamageEvent { Amount = 100 });

            var seen = new List<int>();
            systems.Add(Phase.Reaction, new Probe(ctx =>
                ctx.ProcessEventsWhere<DamageEvent>(e => e.Amount > 50, (_, e) => seen.Add(e.Amount))));

            systems.Tick();

            CollectionAssert.AreEqual(new[] { 100 }, seen);
        }

        [Test]
        public void null_이벤트를_발행하면_던진다()
        {
            var context = new Context();

            Assert.Throws<ArgumentNullException>(() => context.RaiseEvent<DamageEvent>(null));
        }
    }
}
