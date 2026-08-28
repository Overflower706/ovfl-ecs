using System.Collections.Generic;

namespace OVFL.ECS
{
    /// <summary>
    /// 스냅샷용 <see cref="Entity"/> 리스트를 빌려주는 풀.
    /// </summary>
    /// <remarks>
    /// <see cref="ContextEventExtensions.ProcessEvents{T}"/>는 열거 중에 엔티티가 생기거나 죽어도
    /// 안전하도록 목록을 떠 놓고 도는데, 그것을 호출마다 새로 만들면
    /// <b>「이벤트를 읽는 시스템 수 × 프레임」만큼 쓰레기가 난다.</b>
    ///
    /// 정적 버퍼 하나로는 안 된다 — 이벤트를 처리하다 다른 이벤트를 처리하는
    /// <b>중첩 호출</b>이 흔한데, 그러면 바깥쪽이 돌던 목록을 안쪽이 덮어쓴다.
    /// 그래서 스택처럼 빌려주고 돌려받는다.
    ///
    /// 단일 스레드 전용이다. 이 패키지는 어차피 메인 스레드에서만 돈다.
    /// </remarks>
    internal static class EntityListPool
    {
        private static readonly Stack<List<Entity>> Pool = new();

        public static List<Entity> Rent(Context context)
        {
            var list = Pool.Count > 0 ? Pool.Pop() : new List<Entity>();
            list.Clear();
            foreach (var entity in context.AllEntities)
                list.Add(entity);
            return list;
        }

        public static void Return(List<Entity> list)
        {
            // 엔티티 참조를 붙들고 있지 않도록 비워서 돌려받는다.
            list.Clear();
            Pool.Push(list);
        }
    }
}
