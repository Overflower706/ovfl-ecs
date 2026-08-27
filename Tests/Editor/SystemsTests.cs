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
    }
}
