using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    /// <summary>
    /// 엔티티와 컴포넌트를 담는 그릇. 한 세계(월드) 하나에 하나다.
    /// </summary>
    /// <remarks>
    /// <b>불변식 — 엔티티 집합은 <see cref="Phase"/> 경계에서만 바뀐다.</b>
    /// <list type="bullet">
    ///   <item><b>미룬다</b> — <see cref="CreateEntity"/> / <see cref="DestroyEntity"/>.
    ///         열거 중에 저장소가 바뀌면 그 자리에서 터지기 때문이다.</item>
    ///   <item><b>안 미룬다</b> — 컴포넌트 값 쓰기, 살아 있는 엔티티에
    ///         <c>AddComponent</c>/<c>RemoveComponent</c>.
    ///         미루면 자기가 쓴 값을 자기가 못 읽어서 코드가 더 어려워진다.</item>
    /// </list>
    /// </remarks>
    public class Context
    {
        private int[] _entityIndices = new int[1024]; // Sparse 배열
        private readonly List<Entity> _entities = new(); // Dense 배열
        private readonly List<Entity> _pendingAdd = new();
        private readonly List<Entity> _pendingDestroy = new();

        private readonly Queue<Action<Context>> _inbox = new();
        private readonly Queue<Action> _pendingEvents = new();
        private readonly Queue<Action> _pendingFixedEvents = new();

        private readonly Queue<int> _availableIDs = new();
        private readonly List<int> _generations = new();
        private int _nextEntityID = 0;

        /// <summary>
        /// 살아 있는 Entity를 열거합니다. <b>아직 <see cref="Flush"/>되지 않은 것과
        /// 삭제 예약된 것은 나오지 않습니다.</b>
        /// </summary>
        public IEnumerable<Entity> AllEntities
        {
            get
            {
                foreach (var e in _entities)
                    if (e.IsActive) yield return e;
            }
        }

        /// <summary>
        /// <see cref="Systems.Tick"/>이 돈 횟수. 첫 Tick 안에서 읽으면 1입니다.
        /// </summary>
        /// <remarks>
        /// &quot;언제 일어났는가&quot;를 말할 수 있어야 재현이 가능해집니다.
        /// 프레임 시간(<c>Time.time</c>)과 달리 <b>한 스텝 안에서는 값이 고정</b>이므로,
        /// 같은 스텝에 생긴 것들을 하나로 묶어 볼 수 있습니다.
        /// </remarks>
        public uint Tick { get; internal set; }

        /// <summary><see cref="Systems.FixedTick"/>이 돈 횟수. <see cref="Tick"/>과 별개로 셉니다.</summary>
        public uint FixedTick { get; internal set; }

        public Context()
        {
            Array.Fill(_entityIndices, -1);
        }

        // ── 엔티티 ────────────────────────────────────────────────────────

        /// <summary>
        /// Entity를 만듭니다. <b>다음 <see cref="Flush"/>까지는 쿼리에 잡히지 않습니다.</b>
        /// </summary>
        /// <remarks>
        /// 돌려받은 <see cref="Entity"/>에는 <b>지금 바로</b> 컴포넌트를 붙일 수 있습니다.
        /// 미뤄지는 것은 &quot;세계에 등장하는 시점&quot;뿐입니다.
        ///
        /// 즉시 등장시키면 <c>foreach (var e in ctx.AllEntities) ctx.CreateEntity();</c>가
        /// 열거 중 컬렉션 수정으로 터집니다. &quot;그러지 마라&quot;고 적어 두는 대신
        /// 터질 수 없게 만든 것입니다.
        /// </remarks>
        public Entity CreateEntity()
        {
            int id;
            int generation;

            if (_availableIDs.Count > 0)
            {
                id = _availableIDs.Dequeue();
                generation = _generations[id];
            }
            else
            {
                id = _nextEntityID++;
                generation = 1;
                _generations.Add(generation);

                if (id >= _entityIndices.Length)
                {
                    int oldSize = _entityIndices.Length;
                    Array.Resize(ref _entityIndices, oldSize * 2);
                    for (int i = oldSize; i < _entityIndices.Length; i++)
                        _entityIndices[i] = -1;
                }
            }

            var entity = new Entity(id, generation);
            _pendingAdd.Add(entity);
            return entity;
        }

        /// <summary>
        /// Entity를 삭제 예약합니다. <b>쿼리에서는 즉시 사라지고</b>,
        /// 저장소에서 빠지는 것은 다음 <see cref="Flush"/>입니다.
        /// </summary>
        public bool DestroyEntity(Entity entity)
        {
            if (entity == null || !entity.IsActive) return false;

            // 아직 등장하지 않은 것은 등장 자체를 취소한다.
            if (_pendingAdd.Remove(entity))
            {
                entity.IsActive = false;
                _generations[entity.ID]++;
                _availableIDs.Enqueue(entity.ID);
                return true;
            }

            if (!IsAlive(entity)) return false;

            entity.IsActive = false;
            _pendingDestroy.Add(entity);
            return true;
        }

        /// <summary>
        /// 미뤄 둔 생성·삭제를 반영합니다. <see cref="Systems"/>가 Phase 경계마다 부릅니다.
        /// </summary>
        public void Flush()
        {
            if (_pendingAdd.Count > 0)
            {
                foreach (var entity in _pendingAdd)
                {
                    _entities.Add(entity);
                    _entityIndices[entity.ID] = _entities.Count - 1;
                }
                _pendingAdd.Clear();
            }

            FlushDestroyQueue();
        }

        /// <summary>삭제 예약된 Entity를 저장소에서 뺍니다.</summary>
        public void FlushDestroyQueue()
        {
            if (_pendingDestroy.Count == 0) return;

            foreach (var entity in _pendingDestroy)
            {
                int idToRemove = entity.ID;
                int indexToRemove = _entityIndices[idToRemove];
                if (indexToRemove == -1) continue;

                int lastIndex = _entities.Count - 1;

                // Swap & Pop: 맨 뒤의 엔티티를 삭제할 칸으로 이동
                if (indexToRemove != lastIndex)
                {
                    Entity lastEntity = _entities[lastIndex];
                    _entities[indexToRemove] = lastEntity;
                    _entityIndices[lastEntity.ID] = indexToRemove;
                }

                _entities.RemoveAt(lastIndex);
                _entityIndices[idToRemove] = -1;

                _generations[idToRemove]++;
                _availableIDs.Enqueue(idToRemove);
            }

            _pendingDestroy.Clear();
        }

        /// <summary>ID로 Entity를 찾습니다. 살아 있지 않으면 null입니다.</summary>
        /// <remarks>아직 <see cref="Flush"/>되지 않은 것도 찾습니다 — 이미 존재하기 때문입니다.</remarks>
        public Entity GetEntity(int id)
        {
            // 0번 ID도 유효하므로 id >= 0 체크
            if (id < 0 || id >= _generations.Count) return null;

            int index = _entityIndices[id];
            if (index >= 0)
            {
                Entity entity = _entities[index];
                if (entity.Generation != _generations[id]) return null;

                // 삭제 예약된 것은 AllEntities에서도 빠져 있다. 여기만 다르게 답하면
                // 「쿼리에는 없는데 GetEntity로는 잡히는」 엔티티가 생긴다.
                return entity.IsActive ? entity : null;
            }

            // 아직 등장 전. 보통 몇 개뿐이라 훑는다.
            foreach (var pending in _pendingAdd)
                if (pending.ID == id) return pending;

            return null;
        }

        /// <summary>
        /// 이 Entity가 아직 유효한지. <b>아직 등장하지 않은 것도 살아 있는 것으로 봅니다.</b>
        /// </summary>
        /// <remarks>
        /// 만든 순간 존재합니다 — 컴포넌트를 붙일 수도, 값을 읽을 수도 있습니다.
        /// 미뤄지는 것은 <b>쿼리에 잡히는 시점</b>뿐입니다.
        /// </remarks>
        public bool IsAlive(Entity entity)
        {
            if (entity == null || !entity.IsActive) return false;
            if (entity.ID < 0 || entity.ID >= _generations.Count) return false;
            return _generations[entity.ID] == entity.Generation;
        }

        /// <summary>살아 있는 Entity 수. 아직 등장하지 않은 것은 세지 않습니다.</summary>
        public int EntityCount => _entities.Count - _pendingDestroy.Count;

        /// <summary>대기 중인 생성 수. 다음 <see cref="Flush"/>에 등장합니다.</summary>
        public int PendingCount => _pendingAdd.Count;

        /// <summary>모든 Entity를 삭제 예약합니다.</summary>
        public void DestroyAllEntities()
        {
            for (int i = _pendingAdd.Count - 1; i >= 0; i--)
                DestroyEntity(_pendingAdd[i]);

            foreach (var entity in _entities)
                if (entity.IsActive)
                    DestroyEntity(entity);
        }

        // ── 인박스: 밖에서 들어온 변경 ────────────────────────────────────

        /// <summary>
        /// Context를 바꾸는 일을 <b>다음 <see cref="Phase.Inbox"/>까지 미뤄</b> 둡니다.
        /// </summary>
        /// <remarks>
        /// <b>이것이 이 패키지가 네트워크를 견디는 방식이다.</b>
        ///
        /// 네트워크 RPC는 우리가 부르는 것이 아니라 <b>밖에서 아무 때나 불린다.</b>
        /// 호스트에서는 특히 그렇다 — <c>SendTo.ClientsAndHost</c> RPC가 호스트에서는
        /// 그 자리에서 즉시 실행되므로, <b>어떤 시스템이 반쯤 돌던 중에 Context가 바뀐다.</b>
        /// 그러면 같은 스텝 안에서 앞 시스템과 뒤 시스템이 서로 다른 세계를 본다.
        ///
        /// 여기에 넣으면 그 일이 <b>스텝의 정해진 지점에서</b> 일어난다.
        /// 무엇이 언제 적용됐는지 <see cref="Tick"/>으로 말할 수 있게 되고, 재현이 가능해진다.
        ///
        /// <code>
        /// [Rpc(SendTo.ClientsAndHost)]
        /// void ScoreChangedRpc(int value)
        ///     =&gt; context.Enqueue(ctx =&gt; ctx.GetUniqueComponent&lt;ScoreComponent&gt;().Value = value);
        /// </code>
        /// </remarks>
        public void Enqueue(Action<Context> apply)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            _inbox.Enqueue(apply);
        }

        /// <summary>배출을 기다리는 인박스 항목 수.</summary>
        public int InboxCount => _inbox.Count;

        internal void DrainInbox()
        {
            // 배출 도중에 또 들어오는 것은 다음 스텝으로 넘긴다.
            // 안 그러면 매 프레임 도착하는 RPC가 스텝을 영영 끝내지 못하게 만든다.
            int count = _inbox.Count;
            for (int i = 0; i < count; i++)
                _inbox.Dequeue().Invoke(this);
        }

        // ── 이벤트 ────────────────────────────────────────────────────────

        internal void EnqueueEvent(Action publish) => _pendingEvents.Enqueue(publish);
        internal void EnqueueFixedEvent(Action publish) => _pendingFixedEvents.Enqueue(publish);

        /// <summary>발행을 기다리는 이벤트 수.</summary>
        public int PendingEventCount => _pendingEvents.Count;

        internal void PublishEvents()
        {
            int count = _pendingEvents.Count;
            for (int i = 0; i < count; i++)
                _pendingEvents.Dequeue().Invoke();
        }

        internal void PublishFixedEvents()
        {
            int count = _pendingFixedEvents.Count;
            for (int i = 0; i < count; i++)
                _pendingFixedEvents.Dequeue().Invoke();
        }

        internal void DestroyEvents(bool isFixed)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;
                if (entity.TryGetComponent<EventMetadataComponent>(out var meta) && meta.IsFixed == isFixed)
                    DestroyEntity(entity);
            }
        }
    }
}
