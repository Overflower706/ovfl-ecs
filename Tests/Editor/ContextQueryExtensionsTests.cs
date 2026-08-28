using NUnit.Framework;
using OVFL.ECS;
using System.Collections.Generic;

namespace OVFL.ECS.Test
{
    [TestFixture]
    public class ContextQueryExtensionsTests
    {
        class TagComponent : IComponent { }
        class OtherComponent : IComponent { }

        // ───────────────────────────────────────────
        // GetEntitiesWith
        // ───────────────────────────────────────────

        [Test]
        public void GetEntitiesWith_ReturnsEntitiesWithComponent()
        {
            var context = new Context();
            var e1 = context.CreateEntity(); e1.AddComponent<TagComponent>();
            context.CreateEntity(); // TagComponent 없음
            var e3 = context.CreateEntity(); e3.AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var result = new List<Entity>(context.GetEntitiesWith<TagComponent>());

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(e1));
            Assert.IsTrue(result.Contains(e3));
        }

        [Test]
        public void GetEntitiesWith_ReturnsEmpty_WhenNoMatch()
        {
            var context = new Context();
            context.CreateEntity();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var result = new List<Entity>(context.GetEntitiesWith<TagComponent>());

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetEntitiesWith_돌면서_엔티티를_만들어도_터지지_않는다()
        {
            // 1.0.2 전에는 yield return이라 여기서 컬렉션 수정 예외가 났다.
            var context = new Context();
            context.CreateEntity().AddComponent<TagComponent>();
            context.CreateEntity().AddComponent<TagComponent>();
            context.CreateEntity().AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            Assert.DoesNotThrow(() =>
            {
                foreach (var entity in context.GetEntitiesWith<TagComponent>())
                    context.CreateEntity().AddComponent<TagComponent>();
            });

            Assert.AreEqual(3, context.EntityCount, "열거 중에는 집합이 고정이다");
            context.Flush();
            Assert.AreEqual(6, context.EntityCount);
        }

        [Test]
        public void GetEntitiesWith_결과는_부른_시점으로_고정된다()
        {
            var context = new Context();
            context.CreateEntity().AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var result = context.GetEntitiesWith<TagComponent>();
            context.CreateEntity().AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            Assert.AreEqual(1, result.Count, "나중에 만든 것은 이미 뜬 결과에 안 들어온다");
            Assert.AreEqual(2, context.GetEntitiesWith<TagComponent>().Count, "다시 부르면 반영된다");
        }

        [Test]
        public void GetEntitiesWith_돌면서_지워도_터지지_않는다()
        {
            var context = new Context();
            context.CreateEntity().AddComponent<TagComponent>();
            context.CreateEntity().AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            Assert.DoesNotThrow(() =>
            {
                foreach (var entity in context.GetEntitiesWith<TagComponent>())
                    context.DestroyEntity(entity);
            });
            context.FlushDestroyQueue();

            Assert.AreEqual(0, context.GetEntitiesWith<TagComponent>().Count);
        }

        // ───────────────────────────────────────────
        // TryGetUniqueEntity
        // ───────────────────────────────────────────

        [Test]
        public void TryGetUniqueEntity_ReturnsTrue_WhenExactlyOne()
        {
            var context = new Context();
            var e = context.CreateEntity(); e.AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var found = context.TryGetUniqueEntity<TagComponent>(out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(e, result);
        }

        [Test]
        public void TryGetUniqueEntity_ReturnsFalse_WhenNone()
        {
            var context = new Context();

            var found = context.TryGetUniqueEntity<TagComponent>(out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetUniqueEntity_ReturnsFalse_WhenMultiple()
        {
            var context = new Context();
            context.CreateEntity().AddComponent<TagComponent>();
            context.CreateEntity().AddComponent<TagComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var found = context.TryGetUniqueEntity<TagComponent>(out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetUniqueEntity_IgnoresEntitiesWithoutComponent()
        {
            var context = new Context();
            context.CreateEntity().AddComponent<OtherComponent>();
            var target = context.CreateEntity(); target.AddComponent<TagComponent>();
            context.CreateEntity().AddComponent<OtherComponent>();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var found = context.TryGetUniqueEntity<TagComponent>(out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(target, result);
        }

        // ───────────────────────────────────────────
        // TryGetUniqueComponent
        // ───────────────────────────────────────────

        [Test]
        public void TryGetUniqueComponent_ReturnsComponent_WhenExactlyOne()
        {
            var context = new Context();
            var tag = new TagComponent();
            context.CreateEntity().AddComponent(tag);
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var found = context.TryGetUniqueComponent<TagComponent>(out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(tag, result);
        }

        [Test]
        public void TryGetUniqueComponent_ReturnsFalse_WhenNone()
        {
            var context = new Context();

            var found = context.TryGetUniqueComponent<TagComponent>(out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        // ───────────────────────────────────────────
        // TryGetEntityByID
        // ───────────────────────────────────────────

        [Test]
        public void TryGetEntityByID_ReturnsTrue_WhenEntityExists()
        {
            var context = new Context();
            var e = context.CreateEntity();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다

            var found = context.TryGetEntityByID(e.ID, out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(e, result);
        }

        [Test]
        public void TryGetEntityByID_ReturnsFalse_WhenEntityNotFound()
        {
            var context = new Context();

            var found = context.TryGetEntityByID(999, out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetEntityByID_ReturnsFalse_AfterEntityDestroyed()
        {
            var context = new Context();
            var e = context.CreateEntity();
            context.Flush(); // 3.0.0: 생성은 Flush에서 등장한다
            int id = e.ID;
            context.DestroyEntity(e);
            context.FlushDestroyQueue();

            var found = context.TryGetEntityByID(id, out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }
    }
}
