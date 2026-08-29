using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    /// <summary>
    /// 컴포넌트를 담는 그릇. <b>이 객체는 자기가 살아 있는지 모릅니다.</b>
    /// </summary>
    /// <remarks>
    /// <c>ID</c>와 <c>Generation</c>은 <b>손잡이</b>일 뿐이고, 그 손잡이가 아직 유효한지는
    /// <see cref="Context.IsAlive"/>가 답합니다. 엔티티에 「살아 있음」 플래그를 들려 두면
    /// Context가 아는 것과 엔티티가 아는 것이 <b>어긋날 수 있는 두 자리</b>가 생깁니다.
    ///
    /// <c>Generation</c>이 그 역할을 이미 합니다. ID는 재사용되지만 세대는 올라가므로,
    /// 재사용된 ID를 들고 있는 낡은 손잡이도 구분됩니다.
    /// </remarks>
    public class Entity : IEquatable<Entity>
    {
        public readonly int ID;
        public readonly int Generation;
        private readonly Dictionary<Type, IComponent> _components = new();

        public Entity(int id, int generation)
        {
            ID = id;
            Generation = generation;
        }

        /// <summary>
        /// 컴포넌트를 부착합니다. 키는 <b>넘긴 인스턴스의 실제 타입</b>입니다.
        /// </summary>
        /// <remarks>
        /// <c>component.GetType()</c>으로 잡으므로 기반 타입 변수로 넘겨도 제대로 들어갑니다 —
        /// <c>IComponent c = new Foo(); e.AddComponent(c);</c> 뒤에
        /// <c>GetComponent&lt;Foo&gt;()</c>가 그 인스턴스를 돌려줍니다.
        /// </remarks>
        public T AddComponent<T>(T component) where T : class, IComponent
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component), $"{this}에 null 컴포넌트를 부착할 수 없습니다.");

            _components[component.GetType()] = component;
            return component;
        }

        public T AddComponent<T>() where T : class, IComponent, new()
        {
            var component = new T();
            return AddComponent(component);
        }

        public T GetComponent<T>() where T : class, IComponent
        {
            _components.TryGetValue(typeof(T), out var component);
            return component as T;
        }

        public bool TryGetComponent<T>(out T component) where T : class, IComponent
        {
            component = GetComponent<T>();
            return component != null;
        }

        public bool HasComponent<T>() where T : class, IComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        public void RemoveComponent<T>() where T : class, IComponent
        {
            _components.Remove(typeof(T));
        }

        /// <summary>부착된 컴포넌트를 (타입, 인스턴스)로 열거합니다.</summary>
        /// <remarks>
        /// <b>패키지 안에서만 씁니다.</b> 밖으로 열면 「엔티티가 든 것을 훑어 무언가 한다」가 쉬워지고,
        /// 그것은 시스템이 컴포넌트 타입을 정해 놓고 도는 이 패키지의 모양을 흐립니다.
        /// <see cref="ContextSnapshotExtensions.Capture"/>가 이것을 씁니다.
        /// </remarks>
        internal IEnumerable<KeyValuePair<Type, IComponent>> Components => _components;

        /// <summary>「없음」을 뜻하는 자리표. ID가 음수라 어떤 Context에서도 살아 있지 않습니다.</summary>
        public static readonly Entity Null = new Entity(-1, 0);
        public bool IsNull => ID < 0;
        public bool Equals(Entity other)
        {
            if (other is null) return false;

            if (ReferenceEquals(this, other)) return true;

            return ID == other.ID && Generation == other.Generation;
        }
        public override bool Equals(object obj) => obj is Entity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ID, Generation);
        public static bool operator ==(Entity left, Entity right)
        {
            if (left is null) return right is null;

            return left.Equals(right);
        }
        public static bool operator !=(Entity left, Entity right) => !(left == right);
        public override string ToString() => $"Entity({ID}:{Generation})";
    }
}
