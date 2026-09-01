using NUnit.Framework;
using OVFL.ECS;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace OVFL.ECS.Test
{
    [TestFixture]
    public class SystemsTests
    {
        private bool _savedRethrow;

        // 이 픽스처의 기존 테스트들은 「예외를 삼키고 다음 시스템을 계속 돌린다」를
        // 검증한다. 에디터 기본값이 재던지기이므로 여기서 눌러 둔다.
        // 재던지기 쪽은 아래 RethrowOnSystemException_* 테스트가 따로 켜서 본다.
        [SetUp]
        public void SetUp()
        {
            _savedRethrow = Systems.RethrowOnSystemException;
            Systems.RethrowOnSystemException = false;
        }

        [TearDown]
        public void TearDown() => Systems.RethrowOnSystemException = _savedRethrow;

        // 모의 시스템 (Mock System)
        class MockSystem : ISetupSystem, ITickSystem
        {
            public Context Context { get; set; }
            public int SetupCount = 0;
            public int TickCount = 0;
            public List<string> ExecutionLog; // 실행 순서 기록용

            public MockSystem(List<string> log) => ExecutionLog = log;

            public void Setup()
            {
                SetupCount++;
                ExecutionLog.Add("Setup");
            }

            public void Tick()
            {
                TickCount++;
                ExecutionLog.Add("Tick");
            }
        }

        class MockTeardownSystem : ITeardownSystem
        {
            public Context Context { get; set; }
            public int TeardownCount = 0;

            public void Teardown() => TeardownCount++;
        }

        class MockCleanupSystem : ITickSystem, ICleanupSystem
        {
            public Context Context { get; set; }
            public int TickCount = 0;
            public int CleanupCount = 0;

            public void Tick() => TickCount++;
            public void Cleanup() => CleanupCount++;
        }

        class MockFixedTickSystem : IFixedTickSystem
        {
            public Context Context { get; set; }
            public int FixedTickCount = 0;

            public void FixedTick() => FixedTickCount++;
        }

        class ThrowingSetupSystem : ISetupSystem
        {
            public Context Context { get; set; }
            public void Setup() => throw new Exception("Setup 예외");
        }

        class ThrowingTickSystem : ITickSystem
        {
            public Context Context { get; set; }
            public void Tick() => throw new Exception("Tick 예외");
        }

        class ThrowingCleanupSystem : ITickSystem, ICleanupSystem
        {
            public Context Context { get; set; }
            public void Tick() { }
            public void Cleanup() => throw new Exception("Cleanup 예외");
        }

        class ThrowingTeardownSystem : ITeardownSystem
        {
            public Context Context { get; set; }
            public void Teardown() => throw new Exception("Teardown 예외");
        }

        class MockFixedCleanupSystem : IFixedTickSystem, IFixedCleanupSystem
        {
            public Context Context { get; set; }
            public int FixedTickCount = 0;
            public int FixedCleanupCount = 0;

            public void FixedTick() => FixedTickCount++;
            public void FixedCleanup() => FixedCleanupCount++;
        }

        class ThrowingFixedCleanupSystem : IFixedTickSystem, IFixedCleanupSystem
        {
            public Context Context { get; set; }
            public void FixedTick() { }
            public void FixedCleanup() => throw new Exception("FixedCleanup 예외");
        }

        [Test]
        public void SetContext_ShouldInjectContextToAllSystems()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();
            var mockSys = new MockSystem(log);

            systems.Add(Phase.Simulation, mockSys); // AddSystem 시점에 주입됨

            Assert.IsNotNull(mockSys.Context);
            Assert.AreEqual(context, mockSys.Context);
        }

        [Test]
        public void LifeCycle_ShouldRunInOrder()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();

            // 시스템 등록
            systems.Add(Phase.Simulation, new MockSystem(log));

            // 실행
            systems.Setup(); // Log: "Setup"
            systems.Tick();  // Log: "Tick"
            systems.Tick();  // Log: "Tick"

            Assert.AreEqual(3, log.Count);
            Assert.AreEqual("Setup", log[0]);
            Assert.AreEqual("Tick", log[1]);
            Assert.AreEqual("Tick", log[2]);
        }

        [Test]
        public void RemoveSystem_ShouldStopSystemFromRunning()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();
            var mockSys = new MockSystem(log);

            systems.Add(Phase.Simulation, mockSys);
            systems.Tick(); // Tick 1회

#pragma warning disable 618 // 이 테스트의 대상이 그 옛 API다
            systems.RemoveSystem(mockSys);
#pragma warning restore 618
            systems.Tick(); // 제거됐으므로 실행 안 됨

            Assert.AreEqual(1, mockSys.TickCount);
        }

        [Test]
        public void RemoveAllSystems_ShouldClearAllSystems()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();

            systems.Add(Phase.Simulation, new MockSystem(log));
            systems.RemoveAllSystems();

            systems.Setup();
            systems.Tick();

            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void CleanupSystem_ShouldRunWhenCleanupCalled()
        {
            var context = new Context();
            var systems = new Systems(context);
            var mockSys = new MockCleanupSystem();

            systems.Add(Phase.Simulation, mockSys);
            systems.Tick();
            systems.Cleanup();
            systems.Tick();
            systems.Cleanup();

            Assert.AreEqual(2, mockSys.TickCount);
            Assert.AreEqual(2, mockSys.CleanupCount);
        }

        [Test]
        public void FixedTickSystem_ShouldRunOnFixedTick()
        {
            var context = new Context();
            var systems = new Systems(context);
            var mockSys = new MockFixedTickSystem();

            systems.Add(Phase.Simulation, mockSys);
            systems.FixedTick();
            systems.FixedTick();

            Assert.AreEqual(2, mockSys.FixedTickCount);
        }

        [Test]
        public void Setup_WhenOneSystemThrows_OtherSystemsShouldStillRun()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();

            systems.Add(Phase.Simulation, new ThrowingSetupSystem());
            systems.Add(Phase.Simulation, new MockSystem(log));

            LogAssert.Expect(LogType.Exception, "Exception: Setup 예외");
            Assert.DoesNotThrow(() => systems.Setup());
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual("Setup", log[0]);
        }

        [Test]
        public void Tick_WhenOneSystemThrows_OtherSystemsShouldStillRun()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();

            systems.Add(Phase.Simulation, new ThrowingTickSystem());
            systems.Add(Phase.Simulation, new MockSystem(log));

            LogAssert.Expect(LogType.Exception, "Exception: Tick 예외");
            Assert.DoesNotThrow(() => systems.Tick());
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual("Tick", log[0]);
        }

        [Test]
        public void Cleanup_WhenOneSystemThrows_OtherSystemsShouldStillRun()
        {
            var context = new Context();
            var systems = new Systems(context);
            var mockSys = new MockCleanupSystem();

            systems.Add(Phase.Simulation, new ThrowingCleanupSystem());
            systems.Add(Phase.Simulation, mockSys);

            systems.Tick();
            LogAssert.Expect(LogType.Exception, "Exception: Cleanup 예외");
            Assert.DoesNotThrow(() => systems.Cleanup());
            Assert.AreEqual(1, mockSys.CleanupCount);
        }

        [Test]
        public void Teardown_WhenOneSystemThrows_OtherSystemsShouldStillRun()
        {
            var context = new Context();
            var systems = new Systems(context);
            var teardownSys = new MockTeardownSystem();

            systems.Add(Phase.Simulation, new ThrowingTeardownSystem());
            systems.Add(Phase.Simulation, teardownSys);

            LogAssert.Expect(LogType.Exception, "Exception: Teardown 예외");
            Assert.DoesNotThrow(() => systems.Teardown());
            Assert.AreEqual(1, teardownSys.TeardownCount);
        }

        [Test]
        public void FixedCleanupSystem_ShouldRunWhenFixedCleanupCalled()
        {
            var context = new Context();
            var systems = new Systems(context);
            var mockSys = new MockFixedCleanupSystem();

            systems.Add(Phase.Simulation, mockSys);
            systems.FixedTick();
            systems.FixedCleanup();
            systems.FixedTick();
            systems.FixedCleanup();

            Assert.AreEqual(2, mockSys.FixedTickCount);
            Assert.AreEqual(2, mockSys.FixedCleanupCount);
        }

        [Test]
        public void FixedCleanup_WhenOneSystemThrows_OtherSystemsShouldStillRun()
        {
            var context = new Context();
            var systems = new Systems(context);
            var mockSys = new MockFixedCleanupSystem();

            systems.Add(Phase.Simulation, new ThrowingFixedCleanupSystem());
            systems.Add(Phase.Simulation, mockSys);

            systems.FixedTick();
            LogAssert.Expect(LogType.Exception, "Exception: FixedCleanup 예외");
            Assert.DoesNotThrow(() => systems.FixedCleanup());
            Assert.AreEqual(1, mockSys.FixedCleanupCount);
        }

        [Test]
        public void AddSystem_Generic_ShouldCreateAndRegisterSystem()
        {
            var context = new Context();
            var systems = new Systems(context);

            systems.Add<MockFixedTickSystem>(Phase.Simulation);
            systems.FixedTick();
            systems.FixedTick();

            // 예외 없이 2회 실행됐으면 등록 성공
            Assert.DoesNotThrow(() => systems.FixedTick());
        }

        [Test]
        public void Teardown_ShouldRemoveAllSystemsAfterExecution()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();
            var mockSys = new MockSystem(log);
            var teardownSys = new MockTeardownSystem();

            systems.Add(Phase.Simulation, mockSys);
            systems.Add(Phase.Simulation, teardownSys);

            systems.Teardown();

            // Teardown이 실행됐는지 확인
            Assert.AreEqual(1, teardownSys.TeardownCount);

            // Teardown 이후 시스템이 해제됐는지 확인
            systems.Tick();
            Assert.AreEqual(0, mockSys.TickCount);
        }

        // ── 예외 정책과 스텝 카운터 ────────────────────────────────────
        // 파생 클래스가 Add를 가로채는지는 PhaseAndInboxTests가 본다.

        [Test]
        public void RethrowOnSystemException_켜면_호출자까지_올라온다()
        {
            var systems = new Systems(new Context());
            var log = new List<string>();
            systems.Add(Phase.Simulation, new ThrowingTickSystem());
            systems.Add(Phase.Simulation, new MockSystem(log));

            Systems.RethrowOnSystemException = true;

            Assert.Throws<Exception>(() => systems.Tick());
            Assert.AreEqual(0, log.Count, "예외 뒤의 시스템은 돌지 않는다");
        }

        [Test]
        public void RethrowOnSystemException_켜도_Teardown은_시스템을_비운다()
        {
            // 재던지기가 뒤처리를 건너뛰면, 이미 정리된 리소스를 붙든 시스템이
            // 다음 Setup에서 되살아난다.
            var systems = new Systems(new Context());
            var log = new List<string>();
            var mockSys = new MockSystem(log);
            systems.Add(Phase.Simulation, new ThrowingTeardownSystem());
            systems.Add(Phase.Simulation, mockSys);

            Systems.RethrowOnSystemException = true;

            Assert.Throws<Exception>(() => systems.Teardown());

            systems.Tick();
            Assert.AreEqual(0, mockSys.TickCount, "예외가 나도 시스템 목록은 비워진다");
        }

        [Test]
        public void Tick과_FixedTick은_Context의_카운터를_따로_센다()
        {
            var context = new Context();
            var systems = new Systems(context);

            Assert.AreEqual(0u, context.Tick, "시작은 0");
            Assert.AreEqual(0u, context.FixedTick);

            systems.Tick();
            systems.Tick();
            systems.FixedTick();

            Assert.AreEqual(2u, context.Tick);
            Assert.AreEqual(1u, context.FixedTick);
        }

        [Test]
        public void Tick_카운터는_시스템이_도는_동안_이미_올라가_있다()
        {
            // 첫 Tick 안에서 읽으면 1이다. 0이면 「몇 번째 스텝인가」를
            // 시스템이 말할 수 없다.
            var context = new Context();
            var systems = new Systems(context);
            uint seen = uint.MaxValue;
            systems.Add(Phase.Simulation, new TickCounterProbe(v => seen = v));

            systems.Tick();

            Assert.AreEqual(1u, seen);
        }

        class TickCounterProbe : ITickSystem
        {
            public Context Context { get; set; }
            private readonly Action<uint> _sink;
            public TickCounterProbe(Action<uint> sink) => _sink = sink;
            public void Tick() => _sink(Context.Tick);
        }

        // ───────────────────────────────────────────
        // 등록 해제 · Setup의 반영
        // ───────────────────────────────────────────

        [Test]
        public void Remove는_FixedTick_버킷에서도_뺀다()
        {
            // Phase 버킷이 Tick·FixedTick 둘이라, 한쪽만 빼면 죽은 시스템이 계속 돈다.
            var context = new Context();
            var systems = new Systems(context);
            var system = new BothLanes();
            systems.Add(Phase.Simulation, system);

            systems.Tick();
            systems.FixedTick();
            Assert.AreEqual(1, system.TickCount);
            Assert.AreEqual(1, system.FixedTickCount);

            systems.Remove(system);
            systems.Tick();
            systems.FixedTick();

            Assert.AreEqual(1, system.TickCount, "Tick 버킷에서 빠졌다");
            Assert.AreEqual(1, system.FixedTickCount, "FixedTick 버킷에서도 빠졌다");
        }

        class BothLanes : ITickSystem, IFixedTickSystem
        {
            public Context Context { get; set; }
            public int TickCount;
            public int FixedTickCount;
            public void Tick() => TickCount++;
            public void FixedTick() => FixedTickCount++;
        }

        [Test]
        public void Setup에서_만든_엔티티는_Setup이_끝나면_쿼리에_잡힌다()
        {
            // Setup의 finally가 Flush한다. 안 그러면 첫 Tick의 첫 Phase까지 안 보인다.
            var context = new Context();
            var systems = new Systems(context);
            systems.Add(Phase.Simulation, new SpawnOnSetup());

            systems.Setup();

            Assert.AreEqual(1, context.GetEntitiesWith<SpawnedMarker>().Count);
        }

        class SpawnedMarker : IComponent { }

        class SpawnOnSetup : ISetupSystem
        {
            public Context Context { get; set; }
            public void Setup() => Context.CreateEntity().AddComponent(new SpawnedMarker());
        }

        [Test]
        public void 앞_시스템이_Setup에서_만든_엔티티를_뒤_시스템이_Setup에서_본다()
        {
            // 초기화는 세우는 쪽과 읽는 쪽으로 갈린다. 시스템마다 반영하지 않으면
            // 읽는 쪽이 빈 세계를 보고, 예외도 경고도 없이 null이 흘러간다.
            var context = new Context();
            var systems = new Systems(context);
            var reader = new CountOnSetup();
            systems.Add(Phase.Simulation, new SpawnOnSetup());
            systems.Add(Phase.Simulation, reader);

            systems.Setup();

            Assert.AreEqual(1, reader.Seen);
        }

        class CountOnSetup : ISetupSystem
        {
            public Context Context { get; set; }
            public int Seen = -1;
            public void Setup() => Seen = Context.GetEntitiesWith<SpawnedMarker>().Count;
        }

        [Test]
        public void Enqueue에_null을_넣으면_던진다()
        {
            Assert.Throws<ArgumentNullException>(() => new Context().Enqueue(null));
        }

        // ───────────────────────────────────────────
        // 등록의 성질
        // ───────────────────────────────────────────

        class MultiRole : ISetupSystem, ITickSystem, IFixedTickSystem, ICleanupSystem,
                          IFixedCleanupSystem, ITeardownSystem
        {
            public Context Context { get; set; }
            public int SetupCount, TickCount, FixedTickCount, CleanupCount, FixedCleanupCount, TeardownCount;
            public void Setup() => SetupCount++;
            public void Tick() => TickCount++;
            public void FixedTick() => FixedTickCount++;
            public void Cleanup() => CleanupCount++;
            public void FixedCleanup() => FixedCleanupCount++;
            public void Teardown() => TeardownCount++;
        }

        [Test]
        public void 한_시스템이_여러_인터페이스를_구현하면_전부에_들어간다()
        {
            var systems = new Systems(new Context());
            var system = new MultiRole();
            systems.Add(Phase.Simulation, system);

            systems.Setup();
            systems.Tick();
            systems.Cleanup();
            systems.FixedTick();
            systems.FixedCleanup();
            Assert.AreEqual(1, systems.Count, "여럿을 구현해도 시스템은 하나다");

            systems.Teardown();

            Assert.AreEqual(1, system.SetupCount);
            Assert.AreEqual(1, system.TickCount);
            Assert.AreEqual(1, system.FixedTickCount);
            Assert.AreEqual(1, system.CleanupCount);
            Assert.AreEqual(1, system.FixedCleanupCount);
            Assert.AreEqual(1, system.TeardownCount);
        }

        [Test]
        public void Add는_자기를_돌려줘_사슬로_엮인다()
        {
            var systems = new Systems(new Context());
            var returned = systems.Add(Phase.Input, new MultiRole()).Add(Phase.View, new MultiRole());

            Assert.AreSame(systems, returned);
            Assert.AreEqual(2, systems.Count);
        }

        [Test]
        public void 같은_시스템을_두_번_등록하면_두_번_돈다()
        {
            // 막지 않는다. 막으면 「같은 일을 두 Phase에서」가 불가능해진다.
            var systems = new Systems(new Context());
            var system = new MultiRole();

            systems.Add(Phase.Input, system);
            systems.Add(Phase.View, system);
            systems.Tick();

            Assert.AreEqual(2, system.TickCount);
        }

        [Test]
        public void Teardown_뒤에_돌려도_아무것도_안_돈다()
        {
            var systems = new Systems(new Context());
            var system = new MultiRole();
            systems.Add(Phase.Simulation, system);

            systems.Teardown();
            systems.Tick();
            systems.FixedTick();
            systems.Cleanup();

            Assert.AreEqual(0, system.TickCount);
            Assert.AreEqual(0, system.FixedTickCount);
            Assert.AreEqual(0, system.CleanupCount);
            Assert.AreEqual(0, systems.Count);
        }

        [Test]
        public void Setup이_던져도_그_전에_만든_엔티티는_반영된다()
        {
            // finally의 Flush다. 안 그러면 첫 Tick까지 그 엔티티가 안 보인다.
            var context = new Context();
            var systems = new Systems(context);
            systems.Add(Phase.Simulation, new SpawnThenThrow());

            Systems.RethrowOnSystemException = false;
            LogAssert.ignoreFailingMessages = true;
            systems.Setup();
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(1, context.GetEntitiesWith<SpawnedMarker>().Count);
        }

        class SpawnThenThrow : ISetupSystem
        {
            public Context Context { get; set; }
            public void Setup()
            {
                Context.CreateEntity().AddComponent(new SpawnedMarker());
                throw new Exception("Setup 예외");
            }
        }
    }
}
