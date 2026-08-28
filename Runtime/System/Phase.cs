namespace OVFL.ECS
{
    /// <summary>
    /// 시스템이 도는 순서. <b>등록한 줄 순서가 아니라 이 값이 순서를 정한다.</b>
    /// </summary>
    /// <remarks>
    /// <b>왜 필요한가.</b> 이전 판에서는 <c>AddSystem</c>을 부른 순서가 곧 실행 순서였다.
    /// 그래서 한 줄을 위로 옮기는 것만으로 동작이 바뀌는데 <b>컴파일러도 테스트도 모른다.</b>
    /// 순서에 의미가 있다면 그 의미를 타입이 들고 있어야 한다.
    ///
    /// <b>Phase 경계에서 일어나는 일</b> (<see cref="Systems.Tick"/> 참고):
    /// <list type="bullet">
    ///   <item>만들어 둔 엔티티가 살아나고, 지운 엔티티가 저장소에서 빠진다</item>
    ///   <item>발행한 이벤트가 보이기 시작한다</item>
    /// </list>
    /// 따라서 <b>같은 Phase 안에서는 엔티티 집합이 고정</b>이다.
    ///
    /// <b>같은 Phase 안의 순서는 여전히 등록 순서다.</b> 이 enum이 없애는 것은
    /// 「멀리 떨어진 두 시스템의 순서가 우연히 정해지는 것」이지, 순서 자체가 아니다.
    /// 한 Phase 안에서 A가 B보다 먼저여야 한다면 그건 <b>둘을 갈라 놓으라는 신호</b>다.
    /// </remarks>
    public enum Phase
    {
        /// <summary>
        /// 밖에서 들어온 변경을 Context에 넣는다. <see cref="Context.Enqueue"/>로 쌓인 것이
        /// <b>이 Phase 직전에</b> 배출된다.
        /// </summary>
        /// <remarks>
        /// 네트워크 RPC·UI 콜백·비동기 완료처럼 <b>우리가 시점을 못 정하는 것</b>이 여기로 들어온다.
        /// 그것들이 아무 때나 Context를 건드리면 어느 시스템이 반쯤 돌던 중인지 알 수 없다.
        /// </remarks>
        Inbox = 0,

        /// <summary>이번 스텝의 입력을 읽어 컴포넌트에 적는다.</summary>
        Input = 1,

        /// <summary>게임 규칙. 상태를 바꾸는 것은 여기서 한다.</summary>
        Simulation = 2,

        /// <summary>시뮬레이션 결과에 대한 반응. 여기서 읽는 이벤트는 앞 Phase가 발행한 것이다.</summary>
        Reaction = 3,

        /// <summary>화면·사운드에 반영한다. <b>여기서 상태를 바꾸지 않는다.</b></summary>
        View = 4,

        /// <summary>밖으로 내보낸다. RPC 송신·저장 같은 것.</summary>
        Outbox = 5
    }
}
