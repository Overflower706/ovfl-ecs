namespace OVFL.ECS
{
    /// <summary>
    /// Event Entity의 마커 컴포넌트. 스텝 끝의 정리가 이것으로 이벤트를 알아봅니다.
    /// </summary>
    public class EventMetadataComponent : IComponent
    {
        /// <summary>발행된 스텝 (<see cref="Context.Tick"/> 또는 <see cref="Context.FixedTick"/>).</summary>
        /// <remarks>
        /// <b>같은 스텝에 생긴 이벤트는 같은 값</b>을 가지므로
        /// &quot;이것들은 한 스텝에서 함께 일어났다&quot;를 말할 수 있고,
        /// 값 자체가 &quot;몇 번째 스텝인가&quot;에 답합니다.
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
