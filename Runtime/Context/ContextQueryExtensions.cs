using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OVFL.ECS
{
    public static class ContextQueryExtensions
    {
        /// <summary>
        /// T 컴포넌트를 가진 모든 Entity를 <b>확정된 목록으로</b> 반환합니다.
        /// </summary>
        /// <remarks>
        /// 지연 열거(<c>yield return</c>)가 아닙니다. 부르는 시점의 상태를 떠 놓으므로
        /// <b>결과를 돌면서 엔티티를 만들거나 지워도 터지지 않습니다.</b>
        /// 지연이면 그 자리에서 컬렉션 수정 예외가 납니다.
        /// </remarks>
        public static List<Entity> GetEntitiesWith<T>(this Context context) where T : class, IComponent
        {
            var result = new List<Entity>();
            foreach (var entity in context.AllEntities)
            {
                if (entity.HasComponent<T>())
                    result.Add(entity);
            }
            return result;
        }

        /// <summary>
        /// T 컴포넌트를 가진 Entity가 정확히 1개일 때 true를 반환합니다.
        /// </summary>
        /// <remarks>
        /// <b>로그를 남기지 않습니다.</b> <c>Try</c>로 시작하는 API는 「없을 수도 있다」가
        /// 정상인 자리에서 불립니다. 여기서 경고를 찍으면 정상 흐름이 매 프레임 로그를 쌓습니다.
        /// 없는 것이 잘못인지 아닌지는 <c>false</c>를 받은 쪽이 압니다.
        /// </remarks>
        public static bool TryGetUniqueEntity<T>(this Context context, out Entity uniqueEntity) where T : class, IComponent
        {
            uniqueEntity = null;
            Entity foundEntity = null;
            int count = 0;

            foreach (var entity in context.AllEntities)
            {
                if (entity.HasComponent<T>())
                {
                    if (count == 0)
                        foundEntity = entity;
                    count++;

                    if (count > 1)
                        return false;
                }
            }

            if (count == 0)
                return false;

            uniqueEntity = foundEntity;
            return true;
        }

        /// <summary>
        /// T 컴포넌트를 가진 Entity가 정확히 1개일 때 해당 컴포넌트를 반환합니다.
        /// </summary>
        public static bool TryGetUniqueComponent<T>(this Context context, out T uniqueComponent) where T : class, IComponent
        {
            uniqueComponent = null;
            if (context.TryGetUniqueEntity<T>(out var componentEntity))
            {
                uniqueComponent = componentEntity.GetComponent<T>();
                return true;
            }
            return false;
        }

        /// <summary>
        /// ID로 Entity를 조회합니다. 존재하지 않으면 false를 반환합니다.
        /// </summary>
        public static bool TryGetEntityByID(this Context context, int entityID, out Entity entity)
        {
            entity = context.GetEntity(entityID);
            return entity != null;
        }

        /// <summary>
        /// ID로 Entity를 조회합니다. 존재하지 않으면 에러 로그와 함께 null을 반환합니다.
        /// </summary>
        public static Entity GetEntityByID(this Context context, int entityID)
        {
            var entity = context.GetEntity(entityID);
            if (entity == null)
                Debug.LogError($"Entity ID {entityID}를 찾을 수 없습니다.");
            return entity;
        }

        // ───────────────────────────────────────────
        // Obsolete (TryGetUniqueEntity / TryGetUniqueComponent으로 대체)
        // ───────────────────────────────────────────

        // 아직 호출부가 많아 남겨 둔다. 에러 로그를 남기는 동작이 이 둘의 존재 이유이므로
        // 그건 그대로 두고, 호출마다 리스트를 만들던 것만 Try 쪽에 위임해 없앴다.

        [Obsolete("GetUniqueComponent은 TryGetUniqueComponent로 대체되었습니다.")]
        public static T GetUniqueComponent<T>(this Context context) where T : class, IComponent
        {
            if (context.TryGetUniqueComponent<T>(out var component))
                return component;

            Debug.LogError($"{typeof(T).Name}가 Context에 없거나 여러 개입니다.");
            return null;
        }

        [Obsolete("GetUniqueEntityWithComponent은 TryGetUniqueEntity로 대체되었습니다.")]
        public static Entity GetUniqueEntityWithComponent<T>(this Context context) where T : class, IComponent
        {
            if (context.TryGetUniqueEntity<T>(out var entity))
                return entity;

            Debug.LogError($"{typeof(T).Name}가 Context에 없거나 여러 개입니다.");
            return null;
        }
    }
}
