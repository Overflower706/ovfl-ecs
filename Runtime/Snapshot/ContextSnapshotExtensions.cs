using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    public static class ContextSnapshotExtensions
    {
        /// <summary>지금 세계를 뜬다.</summary>
        /// <remarks>
        /// <b>경계에서 부른다.</b> 시스템이 반쯤 돌던 중에 뜨면 그 스텝의 절반만 반영된 세계가 나온다.
        /// 스텝 사이(<see cref="Systems.Tick"/> 전후)나 어느 Phase의 시스템 안이 안전하다 —
        /// 세계는 Phase 경계에서만 바뀌기 때문이다.
        ///
        /// 살아 있는 엔티티의 모든 컴포넌트가 한 줄씩 남고, 값은 <see cref="ISnapshotable"/>을
        /// 구현한 것만 담긴다. 아직 <see cref="Context.Flush"/>되지 않은 엔티티와 삭제 예약된
        /// 엔티티는 <see cref="Context.AllEntities"/>에 안 나오므로 빠진다.
        ///
        /// <b>공짜가 아니다.</b> 엔티티 전수 순회 + 값마다 박싱이다. 매 프레임 도는 자리가 아니라
        /// 디버깅·테스트처럼 <b>일부러 부르는 자리</b>를 위한 것이다.
        /// </remarks>
        public static Snapshot Capture(this Context context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var entries = new List<SnapshotEntry>();
            foreach (var entity in context.AllEntities)
            {
                foreach (var pair in entity.Components)
                {
                    var state = pair.Value is ISnapshotable snapshotable ? snapshotable.Capture() : null;
                    entries.Add(new SnapshotEntry(entity.ID, entity.Generation, pair.Key, state));
                }
            }

            return new Snapshot(context.Tick, entries);
        }
    }
}
