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
            Assert.IsFalse(entity.IsActive); // Entity 객체 내부 상태도 변경
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
        public void Tick_카운터는_Systems가_돌기_전에는_0이다()
        {
            Assert.AreEqual(0u, _context.Tick);
            Assert.AreEqual(0u, _context.FixedTick);
        }
    }
}