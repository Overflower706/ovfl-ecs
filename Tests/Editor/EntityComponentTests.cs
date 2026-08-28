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
        public void EntityNull_IsActive_ShouldBeFalse()
        {
            Assert.IsFalse(Entity.Null.IsActive);
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

        // ── 2.1.0에서 더한 것 ──────────────────────────────────────────

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
    }
}