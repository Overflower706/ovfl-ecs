using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace OVFL.ECS.Test
{
    /// <summary>
    /// 매 프레임 도는 경로는 <b>쓰레기를 만들지 않는다.</b>
    /// 시스템이 수십 개인 게임에서 스텝마다 몇 바이트씩만 새도 초당 메가바이트가 쌓여 GC가 돈다.
    /// 시스템이 스스로 만드는 것(이벤트 발행 등)은 시스템 몫이고, 여기서는 패키지가 끼워 넣는 몫만 본다.
    /// </summary>
    [TestFixture]
    public class AllocationTests
    {
        class Marker : IComponent { }
        class Ping : EventComponent { }

        class Idle : ITickSystem, IFixedTickSystem
        {
            public Context Context { get; set; }
            public int Ticks;
            public void Tick() => Ticks++;
            public void FixedTick() => Ticks++;
        }

        private static Systems Build(out Context context)
        {
            context = new Context();
            for (int i = 0; i < 8; i++) context.CreateEntity().AddComponent(new Marker());
            var systems = new Systems(context);
            foreach (Phase phase in Enum.GetValues(typeof(Phase)))
                for (int i = 0; i < 4; i++) systems.Add(phase, new Idle());
            systems.Setup();
            return systems;
        }

        [Test]
        public void Tick은_할당하지_않는다()
        {
            var systems = Build(out _);
            systems.Tick(); // 첫 호출의 JIT · 정적 초기화는 빼고 본다

            Assert.That(() => systems.Tick(), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void FixedTick은_할당하지_않는다()
        {
            var systems = Build(out _);
            systems.FixedTick();

            Assert.That(() => systems.FixedTick(), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ProcessEvents는_할당하지_않는다()
        {
            var context = new Context();
            for (int i = 0; i < 8; i++) context.CreateEntity().AddComponent(new Marker());
            context.CreateEntity().AddComponent(new Ping());
            context.Flush();
            int seen = 0;
            Action<Entity, Ping> onPing = (_, __) => seen++; // 델리게이트는 부르는 쪽이 한 번 만들어 둔다
            context.ProcessEvents(onPing);

            Assert.That(() => context.ProcessEvents(onPing), Is.Not.AllocatingGCMemory());
            Assert.AreEqual(2, seen);
        }

        [Test]
        public void GetEntitiesWith에_목록을_넘기면_할당하지_않는다()
        {
            var context = new Context();
            for (int i = 0; i < 8; i++) context.CreateEntity().AddComponent(new Marker());
            context.Flush();
            var results = new List<Entity>();
            context.GetEntitiesWith<Marker>(results); // 목록이 처음 자랄 때의 할당은 빼고 본다

            Assert.That(() => { context.GetEntitiesWith<Marker>(results); }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(8, results.Count);
        }

        [Test]
        public void TryGetUniqueEntity는_할당하지_않는다()
        {
            var context = new Context();
            for (int i = 0; i < 8; i++) context.CreateEntity().AddComponent(new Marker());
            context.CreateEntity().AddComponent(new Ping());
            context.Flush();
            context.TryGetUniqueEntity<Ping>(out _);

            Assert.That(() => { context.TryGetUniqueEntity<Ping>(out _); }, Is.Not.AllocatingGCMemory());
        }
    }
}
