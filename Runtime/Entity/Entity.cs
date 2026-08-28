using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    public class Entity : IEquatable<Entity>
    {
        public readonly int ID;
        public readonly int Generation;
        public bool IsActive { get; internal set; }
        private readonly Dictionary<Type, IComponent> _components = new();

        public Entity(int id, int generation)
        {
            ID = id;
            Generation = generation;
            IsActive = true;
        }

        /// <summary>
        /// 컴포넌트를 부착합니다. 키는 <b>넘긴 인스턴스의 실제 타입</b>입니다.
        /// </summary>
        /// <remarks>
        /// 정적 타입(<c>typeof(T)</c>)이 아니라 <c>component.GetType()</c>으로 잡습니다.
        /// 기반 타입 변수로 넘겼을 때 — <c>IComponent c = new Foo(); e.AddComponent(c);</c> —
        /// 키가 <c>IComponent</c>에 박혀 <c>GetComponent&lt;Foo&gt;()</c>가 조용히 null을 주던 것을 막습니다.
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

        public static readonly Entity Null = new Entity(-1, 0) { IsActive = false };
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
