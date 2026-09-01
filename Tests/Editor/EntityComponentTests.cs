using NUnit.Framework;
using OVFL.ECS;

namespace OVFL.ECS.Test
{
    [TestFixture]
    public class EntityComponentTests
    {
        // 테스트용 더미 컴포넌트
        class Position : IComponent { public float x, y; }
        class Velocity : IComponent { public float x, y; }

        [Test]
        public void AddComponent_ShouldStoreAndReturnComponent()
        {
            var entity = new Entity(0, 1);
            var pos = entity.AddComponent<Position>();

            Assert.IsNotNull(pos);
            Assert.IsTrue(entity.HasComponent<Position>());
        }

        [Test]
        public void GetComponent_ShouldReturnCorrectInstance()
        {
            var entity = new Entity(0, 1);
            var originalPos = entity.AddComponent<Position>();
            originalPos.x = 10;

            var retrievedPos = entity.GetComponent<Position>();

            Assert.AreSame(originalPos, retrievedPos); // 참조가 같은지 확인
            Assert.AreEqual(10, retrievedPos.x);
        }

        [Test]
        public void RemoveComponent_ShouldWork()
        {
            var entity = new Entity(0, 1);
            entity.AddComponent<Position>();

            entity.RemoveComponent<Position>();

            Assert.IsFalse(entity.HasComponent<Position>());
            Assert.IsNull(entity.GetComponent<Position>());
        }

        [Test]
        public void Entity_Equality_ShouldCheckIDAndGeneration()
        {
            var e1 = new Entity(1, 1);
            var e2 = new Entity(1, 1);
            var e3 = new Entity(1, 2); // 세대가 다름
            var e4 = new Entity(2, 1); // ID가 다름

            Assert.AreEqual(e1, e2); // 값 동등성 (IEquatable)
            Assert.AreNotEqual(e1, e3);
            Assert.AreNotEqual(e1, e4);

            // HashSet 등에서 키로 쓸 때 중요
            Assert.AreEqual(e1.GetHashCode(), e2.GetHashCode());
        }

        [Test]
        public void TryGetComponent_ShouldReturnTrue_WhenComponentExists()
        {
            var entity = new Entity(0, 1);
            var pos = entity.AddComponent<Position>();
            pos.x = 5;

            var result = entity.TryGetComponent<Position>(out var retrieved);

            Assert.IsTrue(result);
            Assert.AreSame(pos, retrieved);
        }

        [Test]
        public void TryGetComponent_ShouldReturnFalse_WhenComponentMissing()
        {
            var entity = new Entity(0, 1);

            var result = entity.TryGetComponent<Position>(out var retrieved);

            Assert.IsFalse(result);
            Assert.IsNull(retrieved);
        }

        [Test]
        public void AddComponent_ShouldOverwrite_WhenSameTypeRegisteredAgain()
        {
            var entity = new Entity(0, 1);
            var first = entity.AddComponent<Position>();
            var second = new Position();
            entity.AddComponent<Position>(second);

            var retrieved = entity.GetComponent<Position>();

            Assert.AreSame(second, retrieved);
            Assert.AreNotSame(first, retrieved);
        }

        [Test]
        public void EntityNull_IsNull_ShouldBeTrue()
        {
            Assert.IsTrue(Entity.Null.IsNull);
        }

        [Test]
        public void EntityNull_어떤_Context에서도_살아있지_않다()
        {
            Assert.IsTrue(Entity.Null.IsNull);
            Assert.IsFalse(new Context().IsAlive(Entity.Null));
        }

        [Test]
        public void EntityOperator_Equality_NullSafety()
        {
            var e1 = new Entity(0, 1);

            Assert.IsTrue(e1 != null);
            Assert.IsFalse(e1 == null);
            Assert.IsTrue(null != e1);
            Assert.IsFalse(null == e1);
            Assert.IsTrue((Entity)null == null);
        }

        // ── 컴포넌트 키와 인자 검사 ──────────────────────────────────

        [Test]
        public void AddComponent_기반_타입_변수로_넘겨도_실제_타입으로_찾힌다()
        {
            // 키를 typeof(T)로 잡으면 여기서 IComponent에 박혀
            // GetComponent<Position>()이 조용히 null을 준다.
            var entity = new Entity(0, 1);
            IComponent asBase = new Position { x = 3, y = 4 };

            entity.AddComponent(asBase);

            Assert.IsTrue(entity.HasComponent<Position>());
            Assert.AreEqual(3, entity.GetComponent<Position>().x);
        }

        [Test]
        public void AddComponent_null이면_던진다()
        {
            var entity = new Entity(0, 1);

            Assert.Throws<System.ArgumentNullException>(() => entity.AddComponent<Position>(null));
        }

        // ───────────────────────────────────────────
        // 키는 «넘긴 인스턴스의 실제 타입»이다
        // ───────────────────────────────────────────

        class Derived : Position { }

        [Test]
        public void GetComponent_기반_타입으로는_찾히지_않는다()
        {
            // 키가 GetType()이라 Derived로 넣으면 Position으로는 안 나온다.
            // 「상속으로 컴포넌트를 묶는다」가 안 된다는 뜻이다.
            var entity = new Entity(0, 1);
            entity.AddComponent(new Derived());

            Assert.IsTrue(entity.HasComponent<Derived>());
            Assert.IsFalse(entity.HasComponent<Position>());
            Assert.IsNull(entity.GetComponent<Position>());
        }

        [Test]
        public void RemoveComponent_없는_것을_지워도_터지지_않는다()
        {
            var entity = new Entity(0, 1);
            Assert.DoesNotThrow(() => entity.RemoveComponent<Position>());
        }

        [Test]
        public void ComponentCount_붙인_만큼_센다()
        {
            var entity = new Entity(0, 1);
            Assert.AreEqual(0, entity.ComponentCount);

            entity.AddComponent<Position>();
            Assert.AreEqual(1, entity.ComponentCount);

            entity.AddComponent<Velocity>();
            Assert.AreEqual(2, entity.ComponentCount);

            entity.RemoveComponent<Position>();
            Assert.AreEqual(1, entity.ComponentCount);
        }

        [Test]
        public void ComponentCount_같은_타입을_두_번_붙여도_하나다()
        {
            // 키가 타입이라 덮어쓴다. 개수도 그것을 따라야 «HasComponent가 참인 타입의 수»와 어긋나지 않는다.
            var entity = new Entity(0, 1);
            entity.AddComponent(new Position());
            entity.AddComponent(new Position());

            Assert.AreEqual(1, entity.ComponentCount);
        }

        [Test]
        public void ComponentCount_없는_것을_지워도_줄지_않는다()
        {
            var entity = new Entity(0, 1);
            entity.AddComponent<Position>();

            entity.RemoveComponent<Velocity>();

            Assert.AreEqual(1, entity.ComponentCount);
        }

        [Test]
        public void 같은_ID와_세대면_다른_Context에서_만든_것이어도_같다()
        {
            // 손잡이는 값이다. 「어느 Context의 것인가」는 손잡이가 모른다.
            var a = new Context();
            var b = new Context();
            var fromA = a.CreateEntity();
            var fromB = b.CreateEntity();

            Assert.AreEqual(fromA, fromB);
            Assert.AreEqual(fromA.GetHashCode(), fromB.GetHashCode());
        }
    }
}
