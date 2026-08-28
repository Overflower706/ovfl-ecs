using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    public class Context
    {
        private int[] _entityIndices = new int[1024]; // Sparse 배열
        private readonly List<Entity> _entities = new(); // Dense 배열
        private readonly List<Entity> _pendingDestroy = new();

        public IEnumerable<Entity> AllEntities
        {
            get
            {
                foreach (var e in _entities)
                    if (e.IsActive) yield return e;
            }
        }

        private readonly Queue<int> _availableIDs = new();
        private readonly List<int> _generations = new();
        private int _nextEntityID = 0;

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
                    // 영역 확장시 -1로 초기화
                    for (int i = oldSize; i < _entityIndices.Length; i++)
                        _entityIndices[i] = -1;
                }
            }

            var entity = new Entity(id, generation);
            _entities.Add(entity);
            _entityIndices[id] = _entities.Count - 1;

            return entity;
        }

        public bool DestroyEntity(Entity entity)
        {
            if (!IsAlive(entity)) return false;

            entity.IsActive = false;
            _pendingDestroy.Add(entity);
            return true;
        }

        public void FlushDestroyQueue()
        {
            foreach (var entity in _pendingDestroy)
            {
                int idToRemove = entity.ID;
                int indexToRemove = _entityIndices[idToRemove];
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

        /// <summary>
        /// ID로 Entity를 찾습니다. <b><see cref="IsAlive"/>가 false인 것은 돌려주지 않습니다.</b>
        /// </summary>
        public Entity GetEntity(int id)
        {
            // 0번 ID도 유효하므로 id >= 0 체크
            if (id < 0 || id >= _generations.Count) return null;

            int index = _entityIndices[id];
            if (index == -1) return null;

            Entity entity = _entities[index];
            if (entity.Generation != _generations[id]) return null;

            // 삭제 예약된 것은 AllEntities에서도 빠져 있다. 여기만 다르게 답하면
            // 「쿼리에는 없는데 GetEntity로는 잡히는」 엔티티가 생긴다.
            return entity.IsActive ? entity : null;
        }

        public bool IsAlive(Entity entity)
        {
            if (entity == null || !entity.IsActive) return false;
            if (entity.ID < 0 || entity.ID >= _generations.Count) return false;
            return _generations[entity.ID] == entity.Generation;
        }

        /// <summary>현재 활성 상태인 Entity 수를 반환합니다.</summary>
        public int EntityCount => _entities.Count - _pendingDestroy.Count;

        /// <summary>모든 Entity를 삭제 예약합니다. FlushDestroyQueue() 또는 Systems.Cleanup() 호출 시 최종 삭제됩니다.</summary>
        public void DestroyAllEntities()
        {
            foreach (var entity in _entities)
            {
                if (entity.IsActive)
                    DestroyEntity(entity);
            }
        }
    }
}