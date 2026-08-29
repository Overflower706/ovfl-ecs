using System.Linq;
using NUnit.Framework;
using OVFL.ECS;

namespace OVFL.ECS.Test
{
    [TestFixture]
    public class ContextTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
        }

        [Test]
        public void CreateEntity_ShouldAssignCorrectIDAndGeneration()
        {
            var entity = _context.CreateEntity();

            Assert.AreEqual(0, entity.ID);
            Assert.AreEqual(1, entity.Generation); // 1세대부터 시작
            Assert.IsTrue(_context.IsAlive(entity));
        }

        [Test]
        public void DestroyEntity_ShouldMakeEntityDead()
        {
            var entity = _context.CreateEntity();
            bool result = _context.DestroyEntity(entity);

            Assert.IsTrue(result);
            Assert.IsFalse(_context.IsAlive(entity));
            Assert.IsFalse(_context.DestroyEntity(entity), "두 번 지울 수 없다");
        }

        [Test]
        public void CreateEntity_ShouldReuseID_WithIncrementedGeneration()
        {
            // 1. 생성 후 삭제 + 큐 플러시
            var e1 = _context.CreateEntity(); // ID: 0, Gen: 1
            int oldId = e1.ID;
            _context.DestroyEntity(e1);
            _context.FlushDestroyQueue(); // ID가 재사용 풀에 들어감

            // 2. 다시 생성 (ID 재사용 확인)
            var e2 = _context.CreateEntity(); // ID: 0, Gen: 2 (예상)

            Assert.AreEqual(oldId, e2.ID);
            Assert.AreNotEqual(e1.Generation, e2.Generation);
            Assert.AreEqual(e1.Generation + 1, e2.Generation);
        }

        [Test]
        public void OldEntityReference_ShouldNotBeAlive_AfterReuse()
        {
            // "죽은 엔티티 참조 문제" 방지 테스트
            var oldEntity = _context.CreateEntity(); // ID: 0, Gen: 1
            _context.DestroyEntity(oldEntity);

            var newEntity = _context.CreateEntity(); // ID: 0, Gen: 2

            // oldEntity 변수는 여전히 ID 0을 가리키지만, 세대가 다름
            Assert.IsFalse(_context.IsAlive(oldEntity));
            Assert.IsTrue(_context.IsAlive(newEntity));
        }

        [Test]
        public void DestroyEntity_SwapAndPop_ShouldKeepOtherEntitiesValid()
        {
            // Sparse Set 삭제 로직 검증 (중간 삭제 시 인덱스 꼬임 방지)
            var e1 = _context.CreateEntity(); // ID 0
            var e2 = _context.CreateEntity(); // ID 1 (삭제 대상)
            var e3 = _context.CreateEntity(); // ID 2 (맨 뒤)

            _context.DestroyEntity(e2);

            // e2는 죽어야 함
            Assert.IsFalse(_context.IsAlive(e2));

            // e1, e3는 여전히 살아있고 데이터가 올바른지 확인
            Assert.IsTrue(_context.IsAlive(e1));
            Assert.IsTrue(_context.IsAlive(e3));

            // 내부적으로 e3가 e2의 자리로 이동했겠지만, 사용자 입장에선 ID로 조회 가능해야 함
            var retrievedE3 = _context.GetEntity(e3.ID);
            Assert.AreEqual(e3, retrievedE3);
        }

        [Test]
        public void Resize_ShouldHandleMoreThan1024Entities()
        {
            // 배열 확장 테스트
            for (int i = 0; i < 1500; i++)
            {
                _context.CreateEntity();
            }

            var lastEntity = _context.CreateEntity();
            Assert.AreEqual(1500, lastEntity.ID);
            Assert.IsTrue(_context.IsAlive(lastEntity));
        }

        [Test]
        public void DestroyEntity_DuringIteration_ShouldNotThrow()
        {
            // 순회 중 삭제 시 예외 발생 여부 테스트
            _context.CreateEntity();
            _context.CreateEntity();
            _context.CreateEntity();

            Assert.DoesNotThrow(() =>
            {
                foreach (var entity in _context.AllEntities.ToList())
                {
                    _context.DestroyEntity(entity);
                }
                _context.FlushDestroyQueue();
            });

            Assert.IsFalse(_context.AllEntities.Any());
        }

        [Test]
        public void DestroyEntity_ShouldBeExcludedFromAllEntities_BeforeFlush()
        {
            // DestroyEntity 호출 직후 AllEntities에서 제외되는지 확인
            var e1 = _context.CreateEntity();
            var e2 = _context.CreateEntity();
            _context.Flush();

            _context.DestroyEntity(e1);

            var alive = _context.AllEntities.ToList();
            Assert.IsFalse(alive.Contains(e1));
            Assert.IsTrue(alive.Contains(e2));
        }

        [Test]
        public void FlushDestroyQueue_ShouldReuseID()
        {
            // FlushDestroyQueue 후 ID가 재사용되는지 확인
            var e1 = _context.CreateEntity(); // ID: 0
            _context.DestroyEntity(e1);
            _context.FlushDestroyQueue();

            var e2 = _context.CreateEntity(); // ID: 0 재사용
            Assert.AreEqual(e1.ID, e2.ID);
            Assert.AreNotEqual(e1.Generation, e2.Generation);
        }

        [Test]
        public void GetEntity_ShouldReturnNull_WhenIDIsInvalid()
        {
            Assert.IsNull(_context.GetEntity(-1));
            Assert.IsNull(_context.GetEntity(9999));
        }

        // ───────────────────────────────────────────
        // EntityCount
        // ───────────────────────────────────────────

        [Test]
        public void EntityCount_ShouldReturnZero_WhenNoEntities()
        {
            Assert.AreEqual(0, _context.EntityCount);
        }

        [Test]
        public void EntityCount_ShouldReturnActiveEntityCount()
        {
            _context.CreateEntity();
            _context.CreateEntity();
            _context.CreateEntity();

            Assert.AreEqual(0, _context.EntityCount, "아직 등장 전");
            Assert.AreEqual(3, _context.PendingCount);

            _context.Flush();

            Assert.AreEqual(3, _context.EntityCount);
        }

        [Test]
        public void EntityCount_ShouldDecrease_AfterDestroyEntity()
        {
            var e1 = _context.CreateEntity();
            _context.CreateEntity();
            _context.Flush();

            _context.DestroyEntity(e1);

            Assert.AreEqual(1, _context.EntityCount);
        }

        // ───────────────────────────────────────────
        // DestroyAllEntities
        // ───────────────────────────────────────────

        [Test]
        public void DestroyAllEntities_ShouldSetEntityCountToZero()
        {
            _context.CreateEntity();
            _context.CreateEntity();
            _context.CreateEntity();

            _context.DestroyAllEntities();

            Assert.AreEqual(0, _context.EntityCount);
        }

        [Test]
        public void DestroyAllEntities_ShouldClearAllEntities_AfterFlush()
        {
            _context.CreateEntity();
            _context.CreateEntity();

            _context.DestroyAllEntities();
            _context.FlushDestroyQueue();

            Assert.IsFalse(System.Linq.Enumerable.Any(_context.AllEntities));
        }

        [Test]
        public void DestroyAllEntities_OnEmptyContext_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => _context.DestroyAllEntities());
        }

        // ── 조회와 스텝 카운터 ────────────────────────────────────────

        [Test]
        public void GetEntity_삭제_예약된_엔티티는_돌려주지_않는다()
        {
            // AllEntities에서는 이미 빠져 있는데 GetEntity로는 잡히면,
            // 「쿼리에는 없는데 ID로는 있는」 엔티티가 생긴다.
            var entity = _context.CreateEntity();
            int id = entity.ID;
            Assert.AreSame(entity, _context.GetEntity(id));

            _context.DestroyEntity(entity);

            Assert.IsNull(_context.GetEntity(id), "FlushDestroyQueue 전에도 이미 없다");
            Assert.IsFalse(_context.IsAlive(entity));
        }

        [Test]
        public void 살아있음은_Context가_판단한다()
        {
            // Entity는 자기가 살아 있는지 모른다. 세대 하나가 판별자다.
            var entity = _context.CreateEntity();
            var other = new Context();

            Assert.IsTrue(_context.IsAlive(entity));
            Assert.IsFalse(other.IsAlive(entity), "다른 Context에서는 살아 있지 않다");
        }

        [Test]
        public void 지운_뒤에도_손잡이로_컴포넌트는_읽힌다()
        {
            // 죽는 것은 Context와의 연결이다. 객체 자체는 GC가 가져갈 때까지 남는다.
            var entity = _context.CreateEntity();
            entity.AddComponent(new Marker());
            _context.Flush();

            _context.DestroyEntity(entity);

            Assert.IsFalse(_context.IsAlive(entity));
            Assert.IsNotNull(entity.GetComponent<Marker>(), "손잡이를 들고 있으면 읽을 수는 있다");
            Assert.IsNull(_context.GetEntity(entity.ID), "Context를 통해서는 못 찾는다");
        }

        class Marker : IComponent { }

        [Test]
        public void Tick_카운터는_Systems가_돌기_전에는_0이다()
        {
            Assert.AreEqual(0u, _context.Tick);
            Assert.AreEqual(0u, _context.FixedTick);
        }

        // ───────────────────────────────────────────
        // 손잡이의 경계 — 두 번 지우기, 남의 것, null
        // ───────────────────────────────────────────

        [Test]
        public void DestroyEntity_두_번_지우면_두_번째는_false다()
        {
            var entity = _context.CreateEntity();
            _context.Flush();

            Assert.IsTrue(_context.DestroyEntity(entity));
            Assert.IsFalse(_context.DestroyEntity(entity), "세대가 이미 올라가서 낡은 손잡이다");
        }

        [Test]
        public void DestroyEntity_다른_Context의_엔티티는_false다()
        {
            // 손잡이는 값일 뿐이라 어느 Context의 것인지 자기가 모른다.
            // 판단은 세대를 가진 Context가 한다.
            var other = new Context();
            var entity = other.CreateEntity();
            other.Flush();

            Assert.IsFalse(_context.DestroyEntity(entity));
            Assert.IsTrue(other.IsAlive(entity), "남의 Context가 지우지 못한다");
        }

        [Test]
        public void IsAlive_null이면_false다()
        {
            Assert.IsFalse(_context.IsAlive(null));
        }

        [Test]
        public void GetEntity_음수_ID면_null이다()
        {
            Assert.IsNull(_context.GetEntity(-1));
            Assert.IsNull(_context.GetEntity(Entity.Null.ID));
        }

        [Test]
        public void Flush_두_번_불러도_결과가_같다()
        {
            _context.CreateEntity();
            _context.Flush();
            _context.Flush();

            Assert.AreEqual(1, _context.EntityCount);
            Assert.AreEqual(1, _context.AllEntities.Count());
        }

        [Test]
        public void PendingCount는_등장을_기다리는_수다()
        {
            _context.CreateEntity();
            _context.CreateEntity();

            Assert.AreEqual(2, _context.PendingCount);
            Assert.AreEqual(0, _context.AllEntities.Count(), "아직 쿼리에 안 잡힌다");

            _context.Flush();
            Assert.AreEqual(0, _context.PendingCount);
            Assert.AreEqual(2, _context.AllEntities.Count());
        }

        [Test]
        public void DestroyAllEntities_등장_전인_것도_함께_지운다()
        {
            _context.CreateEntity();
            _context.Flush();
            _context.CreateEntity(); // 아직 pending

            _context.DestroyAllEntities();
            _context.Flush();

            Assert.AreEqual(0, _context.EntityCount);
            Assert.AreEqual(0, _context.PendingCount);
        }

        [Test]
        public void AllEntities는_지연_열거라_도중에_Flush하면_터진다()
        {
            // 이것이 GetEntitiesWith가 목록을 떠서 주는 이유다.
            _context.CreateEntity();
            _context.Flush();

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                foreach (var _ in _context.AllEntities)
                {
                    _context.CreateEntity();
                    _context.Flush();
                }
            });
        }
    }
}
