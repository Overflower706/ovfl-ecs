namespace OVFL.ECS
{
    /// <summary>
    /// 자기 상태를 <b>값 하나로</b> 낼 수 있는 컴포넌트.
    /// </summary>
    /// <remarks>
    /// <b>왜 오픈인인가.</b> <see cref="IComponent"/>에는 제약이 없어서, 패키지는 임의의 구현체를
    /// 어떻게 복사하는지 알 방법이 없다. 컴포넌트가 <c>MonoBehaviour</c>일 수도 있고 Unity 오브젝트
    /// 참조를 들고 있을 수도 있다. <b>무엇이 「상태」인지는 그 컴포넌트만 안다.</b>
    ///
    /// <b>구현하지 않아도 스냅샷에는 남는다.</b> <see cref="ContextSnapshotExtensions.Capture"/>는
    /// 모든 컴포넌트의 <b>있고 없음</b>을 적고, 값은 이것을 구현한 것만 담는다. 그래서 태그처럼
    /// 필드가 없는 컴포넌트도 붙고 떨어진 것이 <see cref="Snapshot.Diff"/>에 잡힌다.
    ///
    /// <b>낸 값은 뜬 시점에 얼어 있어야 한다.</b> 살아 있는 컬렉션이나 컴포넌트 자기 자신을
    /// 돌려주면 나중에 비교할 때 양쪽이 같은 것을 가리켜 <b>변화가 사라진다.</b>
    /// 그래서 struct를 권장하고, 컬렉션은 복사해서 담는다.
    ///
    /// <code>
    /// public class ScoreComponent : IComponent, ISnapshotable
    /// {
    ///     public int Value;
    ///     public readonly struct State { public readonly int Value; public State(int v) => Value = v; }
    ///     public object Capture() => new State(Value);
    /// }
    /// </code>
    ///
    /// 비교는 <see cref="object.Equals(object)"/>가 한다. struct는 기본 구현이 필드별로 보지만
    /// 리플렉션을 타므로, 자주 뜨는 것이면 <c>IEquatable&lt;T&gt;</c>를 같이 구현한다.
    /// </remarks>
    public interface ISnapshotable
    {
        /// <summary>지금 상태를 값으로 낸다.</summary>
        object Capture();
    }
}
