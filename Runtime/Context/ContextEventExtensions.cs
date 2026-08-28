using System;

namespace OVFL.ECS
{
    /// <summary>
    /// 시스템 사이의 단방향 통신. 발행은 언제든 하고, <b>읽는 것은 다음 Phase부터</b>입니다.
    /// </summary>
    /// <remarks>
    /// <b>왜 바로 안 보이나.</b> 발행하자마자 보이면 &quot;누가 먼저 등록됐나&quot;에 따라
    /// 이벤트를 보기도 하고 못 보기도 한다. 발행을 <see cref="Phase"/> 경계로 미루면
    /// <b>같은 Phase 안의 등록 순서가 이벤트를 통해서는 결과를 바꾸지 못한다.</b>
    ///
    /// 이벤트는 <b>그 스텝 끝에 사라진다.</b> 남겨두면 다음 스텝이 지난 이벤트를 또 읽는다.
    /// </remarks>
    public static class ContextEventExtensions
    {
        internal static Entity CreateEvent<T>(this Context context, T eventComponent, bool isFixed) where T : EventComponent
        {
            var entity = context.CreateEntity();
            entity.AddComponent(eventComponent);
            entity.AddComponent(new EventMetadataComponent
            {
                CreatedTick = isFixed ? context.FixedTick : context.Tick,
                EventTypeName = typeof(T).Name,
                IsFixed = isFixed,
#if UNITY_EDITOR
                StackTrace = Environment.StackTrace
#endif
            });
            return entity;
        }

        /// <summary>이벤트를 발행합니다. <b>다음 <see cref="Phase"/> 경계부터</b> 읽힙니다.</summary>
        public static void RaiseEvent<T>(this Context context, T eventComponent) where T : EventComponent
        {
            if (eventComponent == null) throw new ArgumentNullException(nameof(eventComponent));
            context.EnqueueEvent(() => context.CreateEvent(eventComponent, isFixed: false));
        }

        /// <summary><see cref="Systems.FixedTick"/> 주기의 이벤트를 발행합니다.</summary>
        public static void RaiseFixedEvent<T>(this Context context, T eventComponent) where T : EventComponent
        {
            if (eventComponent == null) throw new ArgumentNullException(nameof(eventComponent));
            context.EnqueueFixedEvent(() => context.CreateEvent(eventComponent, isFixed: true));
        }

        /// <summary>발행된 T 이벤트를 모두 처리합니다. 여러 시스템이 같은 이벤트를 읽어도 됩니다.</summary>
        public static void ProcessEvents<T>(this Context context, Action<Entity, T> action) where T : EventComponent
        {
            var snapshot = EntityListPool.Rent(context);
            try
            {
                foreach (var entity in snapshot)
                {
                    if (entity.TryGetComponent<T>(out var eventComponent))
                        action(entity, eventComponent);
                }
            }
            finally { EntityListPool.Return(snapshot); }
        }

        /// <summary>조건에 맞는 이벤트만 처리합니다.</summary>
        public static void ProcessEventsWhere<T>(this Context context, Func<T, bool> predicate, Action<Entity, T> action) where T : EventComponent
        {
            var snapshot = EntityListPool.Rent(context);
            try
            {
                foreach (var entity in snapshot)
                {
                    if (entity.TryGetComponent<T>(out var eventComponent) && predicate(eventComponent))
                        action(entity, eventComponent);
                }
            }
            finally { EntityListPool.Return(snapshot); }
        }
    }
}
