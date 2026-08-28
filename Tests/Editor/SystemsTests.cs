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

            systems.AddSystem(mockSys); // AddSystem 시점에 주입됨

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
            systems.AddSystem(new MockSystem(log));

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

            systems.AddSystem(mockSys);
            systems.Tick(); // Tick 1회

            systems.RemoveSystem(mockSys);
            systems.Tick(); // 제거됐으므로 실행 안 됨

            Assert.AreEqual(1, mockSys.TickCount);
        }

        [Test]
        public void RemoveAllSystems_ShouldClearAllSystems()
        {
            var context = new Context();
            var systems = new Systems(context);
            var log = new List<string>();

            systems.AddSystem(new MockSystem(log));
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

            systems.AddSystem(mockSys);
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

            systems.AddSystem(mockSys);
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

            systems.AddSystem(new ThrowingSetupSystem());
            systems.AddSystem(new MockSystem(log));

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

            systems.AddSystem(new ThrowingTickSystem());
            systems.AddSystem(new MockSystem(log));

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

            systems.AddSystem(new ThrowingCleanupSystem());
            systems.AddSystem(mockSys);

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

            systems.AddSystem(new ThrowingTeardownSystem());
            systems.AddSystem(teardownSys);

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

            systems.AddSystem(mockSys);
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

            systems.AddSystem(new ThrowingFixedCleanupSystem());
            systems.AddSystem(mockSys);

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

            systems.AddSystem<MockFixedTickSystem>();
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

            systems.AddSystem(mockSys);
            systems.AddSystem(teardownSys);

            systems.Teardown();

            // Teardown이 실행됐는지 확인
            Assert.AreEqual(1, teardownSys.TeardownCount);

            // Teardown 이후 시스템이 해제됐는지 확인
            systems.Tick();
            Assert.AreEqual(0, mockSys.TickCount);
        }

        // ── 2.1.0에서 더한 것 ──────────────────────────────────────────

        /// <summary>AddSystem을 재정의해 가로채는 파생 클래스. 가상 호출을 재는 탐침이다.</summary>
        class CountingSystems : Systems
        {
            public int AddCalls;
            public CountingSystems(Context context) : base(context) { }

            public override Systems AddSystem(ISystem system)
            {
                AddCalls++;
                return base.AddSystem(system);
            }
        }

        [Test]
        public void AddSystem_파생_클래스의_재정의를_탄다()
        {
            var systems = new CountingSystems(new Context());

            systems.AddSystem(new MockTeardownSystem());

            Assert.AreEqual(1, systems.AddCalls);
        }

        [Test]
        public void AddSystem_제네릭_오버로드도_같은_재정의를_탄다()
        {
            // 두 오버로드 중 하나만 가상이면 제네릭으로 넣은 시스템만
            // 파생 클래스를 건너뛴다. 그 어긋남을 막는 테스트다.
            var systems = new CountingSystems(new Context());

            systems.AddSystem<MockTeardownSystem>();

            Assert.AreEqual(1, systems.AddCalls);
        }

        [Test]
        public void RethrowOnSystemException_켜면_호출자까지_올라온다()
        {
            var systems = new Systems(new Context());
            var log = new List<string>();
            systems.AddSystem(new ThrowingTickSystem());
            systems.AddSystem(new MockSystem(log));

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
            systems.AddSystem(new ThrowingTeardownSystem());
            systems.AddSystem(mockSys);

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
            systems.AddSystem(new TickCounterProbe(v => seen = v));

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
    }
}
