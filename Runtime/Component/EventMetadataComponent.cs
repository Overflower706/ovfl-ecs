namespace OVFL.ECS
{
    /// <summary>
    /// Event Entity의 마커 컴포넌트. 스텝 끝의 정리가 이것으로 이벤트를 알아봅니다.
    /// </summary>
    public class EventMetadataComponent : IComponent
    {
        /// <summary>발행된 스텝 (<see cref="Context.Tick"/> 또는 <see cref="Context.FixedTick"/>).</summary>
        /// <remarks>
        /// 프레임 시간이 아니라 스텝 번호입니다. <b>같은 스텝에 생긴 이벤트는 같은 값</b>을 갖고,
        /// 그래서 &quot;이 이벤트들은 한 스텝에서 함께 일어났다&quot;를 말할 수 있습니다.
        /// <c>Time.time</c>은 한 스텝 안에서도 같은 값이라 구분에 쓸 수 없었고,
        /// 무엇보다 &quot;몇 번째 스텝인가&quot;를 답하지 못했습니다.
        /// </remarks>
        public uint CreatedTick { get; set; }

        /// <summary>Event 타입 이름 (디버깅용)</summary>
        public string EventTypeName { get; set; }

        /// <summary>FixedTick 주기 이벤트 여부.</summary>
        public bool IsFixed { get; set; }

#if UNITY_EDITOR
        /// <summary>Event 생성 위치 StackTrace (에디터 전용)</summary>
        public string StackTrace { get; set; }
#endif
    }
}
