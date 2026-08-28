using NUnit.Framework;
using OVFL.ECS;

namespace OVFL.ECS.Test
{
    [TestFixture]
    public class IntegrationTest
    {
        class Health : IComponent { public int Value; }

        class DamageSystem : ITickSystem
        {
            public Context Context { get; set; }

            public void Tick()
            {
                // 실제 구현에서는 루프를 돌겠지만, 여기선 ID 0번을 직접 조회해서 테스트
                var entity = Context.GetEntity(0);
                if (entity != null && entity.TryGetComponent<Health>(out var health))
                {
                    health.Value -= 10;
                }
            }
        }

        [Test]
        public void System_ShouldModify_ComponentData()
        {
            // 1. 준비
            var context = new Context();
            var systems = new Systems(context);

            systems.Add(Phase.Simulation, new DamageSystem());

            // 2. 엔티티 생성 및 데이터 설정
            var player = context.CreateEntity(); // ID: 0
            var hp = player.AddComponent<Health>();
            hp.Value = 100;

            // 3. 틱 실행 (시스템 동작)
            systems.Tick();

            // 4. 검증
            Assert.AreEqual(90, hp.Value);

            systems.Tick();
            Assert.AreEqual(80, hp.Value);
        }
    }
}