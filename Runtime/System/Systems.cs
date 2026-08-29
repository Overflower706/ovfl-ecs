using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    /// <summary>
    /// 시스템을 모아 <see cref="Phase"/> 순서로 돌립니다.
    /// </summary>
    /// <remarks>
    /// <b>한 스텝(<see cref="Tick"/>)에서 일어나는 일:</b>
    /// <code>
    /// Tick++
    /// Phase마다:
    ///     인박스 배출 · 이벤트 발행 · Flush     ← 경계
    ///     그 Phase의 시스템들을 등록 순서로 실행
    /// 마지막에: Flush · 이번 스텝의 이벤트 정리 · Flush
    /// </code>
    /// 경계가 Phase 앞에 있으므로 <b>앞 Phase가 만든 것은 뒤 Phase에서 보이고,
    /// 같은 Phase 안에서는 엔티티 집합이 고정</b>입니다.
    /// </remarks>
    public class Systems
    {
        private static readonly Phase[] PhaseOrder =
        {
            Phase.Inbox, Phase.Input, Phase.Simulation, Phase.Reaction, Phase.View, Phase.Outbox
        };

        private readonly Context context;
        private readonly List<ISystem> allSystems = new();

        // Phase별 버킷. 인덱스가 곧 Phase 값이다.
        private readonly List<ITickSystem>[] tickSystems;
        private readonly List<IFixedTickSystem>[] fixedTickSystems;

        private readonly List<ISetupSystem> setupSystems = new();
        private readonly List<ICleanupSystem> cleanupSystems = new();
        private readonly List<IFixedCleanupSystem> fixedCleanupSystems = new();
        private readonly List<ITeardownSystem> teardownSystems = new();

        /// <summary>
        /// 시스템이 던진 예외를 호출자에게 다시 던질지. 기본값은 <b>에디터에서 true</b>입니다.
        /// </summary>
        /// <remarks>
        /// false면 예외를 로그로만 남기고 다음 시스템을 계속 돌립니다. 그러면 게임은 버티지만
        /// <b>증상이 원인에서 멀어집니다</b> — 죽은 시스템이 갱신하지 못한 값을 뒤 시스템이 읽고
        /// 엉뚱한 곳에서 터지기 때문입니다. 개발 중에는 첫 예외에서 멈추는 편이 낫고,
        /// 빌드에서는 한 시스템의 실패로 게임이 멈추지 않는 편이 낫습니다.
        /// </remarks>
        public static bool RethrowOnSystemException =
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        public Systems(Context context)
        {
            this.context = context;

            int phaseCount = PhaseOrder.Length;
            tickSystems = new List<ITickSystem>[phaseCount];
            fixedTickSystems = new List<IFixedTickSystem>[phaseCount];
            for (int i = 0; i < phaseCount; i++)
            {
                tickSystems[i] = new List<ITickSystem>();
                fixedTickSystems[i] = new List<IFixedTickSystem>();
            }
        }

        // ── 등록 ──────────────────────────────────────────────────────────

        /// <summary>시스템을 그 <see cref="Phase"/>에 등록합니다.</summary>
        public virtual Systems Add(Phase phase, ISystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            allSystems.Add(system);
            system.Context = context;

            int p = (int)phase;

            if (system is ITickSystem tickSystem)
                tickSystems[p].Add(tickSystem);

            if (system is IFixedTickSystem fixedTickSystem)
                fixedTickSystems[p].Add(fixedTickSystem);

            // 아래 넷은 Phase와 무관하다 — 스텝 전체에 한 번씩 도는 것들이다.
            if (system is ISetupSystem setupSystem)
                setupSystems.Add(setupSystem);

            if (system is ICleanupSystem cleanupSystem)
                cleanupSystems.Add(cleanupSystem);

            if (system is IFixedCleanupSystem fixedCleanupSystem)
                fixedCleanupSystems.Add(fixedCleanupSystem);

            if (system is ITeardownSystem teardownSystem)
                teardownSystems.Add(teardownSystem);

            return this;
        }

        /// <remarks>
        /// <see cref="Add(Phase, ISystem)"/>이 <c>virtual</c>이므로 여기서도 파생 클래스의
        /// 재정의를 탑니다. 둘 중 하나만 가상이면 <b>제네릭으로 넣은 시스템만 파생을 건너뛰는</b>
        /// 어긋남이 생깁니다.
        /// </remarks>
        public virtual Systems Add<T>(Phase phase) where T : ISystem, new() => Add(phase, new T());

        /// <summary>Phase 없이 등록하던 옛 API. <see cref="Phase.Simulation"/>으로 들어갑니다.</summary>
        /// <remarks>
        /// <b>이주용으로만 남겨 둡니다.</b> 전부 <see cref="Add(Phase, ISystem)"/>로 옮긴 뒤
        /// 다음 메이저에서 지웁니다. 그때까지는 옛 코드가 컴파일은 되되 경고로 남습니다.
        /// </remarks>
        [Obsolete("Add(Phase, ISystem)을 쓰세요. Phase를 정하지 않으면 순서가 등록 줄에 묶입니다.")]
        public Systems AddSystem(ISystem system) => Add(Phase.Simulation, system);

        /// <inheritdoc cref="AddSystem(ISystem)"/>
        [Obsolete("Add<T>(Phase)를 쓰세요. Phase를 정하지 않으면 순서가 등록 줄에 묶입니다.")]
        public Systems AddSystem<T>() where T : ISystem, new() => Add(Phase.Simulation, new T());

        public virtual Systems Remove(ISystem system)
        {
            allSystems.Remove(system);

            for (int p = 0; p < PhaseOrder.Length; p++)
            {
                if (system is ITickSystem tickSystem)
                    tickSystems[p].Remove(tickSystem);
                if (system is IFixedTickSystem fixedTickSystem)
                    fixedTickSystems[p].Remove(fixedTickSystem);
            }

            if (system is ISetupSystem setupSystem)
                setupSystems.Remove(setupSystem);
            if (system is ICleanupSystem cleanupSystem)
                cleanupSystems.Remove(cleanupSystem);
            if (system is IFixedCleanupSystem fixedCleanupSystem)
                fixedCleanupSystems.Remove(fixedCleanupSystem);
            if (system is ITeardownSystem teardownSystem)
                teardownSystems.Remove(teardownSystem);

            return this;
        }

        [Obsolete("Remove(ISystem)을 쓰세요.")]
        public Systems RemoveSystem(ISystem system) => Remove(system);

        public virtual void RemoveAllSystems()
        {
            allSystems.Clear();
            for (int p = 0; p < PhaseOrder.Length; p++)
            {
                tickSystems[p].Clear();
                fixedTickSystems[p].Clear();
            }
            setupSystems.Clear();
            cleanupSystems.Clear();
            fixedCleanupSystems.Clear();
            teardownSystems.Clear();
        }

        /// <summary>등록된 시스템 수.</summary>
        public int Count => allSystems.Count;

        // ── 실행 ──────────────────────────────────────────────────────────

        /// <summary>모든 Setup System을 실행합니다 (초기화 시 한 번).</summary>
        public void Setup()
        {
            try
            {
                foreach (var system in setupSystems)
                    Run(() => system.Setup());
            }
            finally { context?.Flush(); }
        }

        /// <summary>한 스텝을 돌립니다. Phase 순서로 실행하고 경계마다 반영합니다.</summary>
        public void Tick()
        {
            if (context != null) context.Tick++;

            try
            {
                for (int p = 0; p < PhaseOrder.Length; p++)
                {
                    Boundary(p, isFixed: false);

                    var bucket = tickSystems[p];
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var system = bucket[i];
                        Run(() => system.Tick());
                    }
                }
            }
            finally
            {
                context?.Flush();
                // 이번 스텝의 이벤트는 이번 스텝에서만 산다. 남기면 다음 스텝이
                // 지난 이벤트를 또 읽는다.
                context?.DestroyEvents(isFixed: false);
                context?.Flush();
            }
        }

        /// <summary>FixedUpdate 주기의 한 스텝.</summary>
        public void FixedTick()
        {
            if (context != null) context.FixedTick++;

            try
            {
                for (int p = 0; p < PhaseOrder.Length; p++)
                {
                    Boundary(p, isFixed: true);

                    var bucket = fixedTickSystems[p];
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        var system = bucket[i];
                        Run(() => system.FixedTick());
                    }
                }
            }
            finally
            {
                context?.Flush();
                context?.DestroyEvents(isFixed: true);
                context?.Flush();
            }
        }

        /// <summary>모든 Cleanup System을 실행합니다. <see cref="Tick"/> 이후에 부릅니다.</summary>
        public void Cleanup()
        {
            context?.Flush();
            try
            {
                foreach (var system in cleanupSystems)
                    Run(() => system.Cleanup());
            }
            // 예외를 다시 던지더라도 반영은 한다. 안 그러면 죽은 엔티티가 다음 스텝까지
            // 남아, 원래 예외와 무관한 곳에서 두 번째 사고가 난다.
            finally { context?.Flush(); }
        }

        /// <summary>모든 FixedCleanup System을 실행합니다. <see cref="FixedTick"/> 이후에 부릅니다.</summary>
        public void FixedCleanup()
        {
            context?.Flush();
            try
            {
                foreach (var system in fixedCleanupSystems)
                    Run(() => system.FixedCleanup());
            }
            finally { context?.Flush(); }
        }

        /// <summary>모든 Teardown System을 실행합니다. 끝나면 시스템 목록을 비웁니다.</summary>
        public void Teardown()
        {
            try
            {
                foreach (var system in teardownSystems)
                    Run(() => system.Teardown());
            }
            // Teardown이 실패해도 시스템 목록은 반드시 비운다. 남겨두면 이미 정리된
            // 리소스를 붙든 시스템이 다음 Setup에서 되살아난다.
            finally
            {
                context?.Flush();
                RemoveAllSystems();
            }
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        /// <summary>Phase 경계. 여기서만 세계가 바뀐다.</summary>
        /// <remarks>
        /// 두 레인이 같은 코드를 탄다. 갈라 두면 한쪽만 고치는 사고가 난다.
        /// </remarks>
        private void Boundary(int phaseIndex, bool isFixed)
        {
            if (context == null) return;

            // 인박스는 Tick 레인이 소유한다. 첫 Phase 앞에서 한 번만 배출한다.
            //
            // Phase마다 배출하면 「밖에서 온 변경이 언제 적용됐는가」에 답이 여섯 개가 되고,
            // 두 레인이 나눠 배출하면 인박스 안에서 RaiseEvent로 낸 것이 어느 큐로 갈지가
            // RPC 도착 시점에 따라 갈린다 — FixedTick이 배출하면 그 이벤트는 PublishFixedEvents가
            // 부르지 않으므로 다음 Tick 경계까지 잠든다. 넣는 쪽이 피할 방법이 없는 어긋남이다.
            //
            // 대가는 밖에서 온 변경을 FixedTick이 최대 한 프레임 늦게 본다는 것이다.
            // 물리 레인은 자기 상태로 돌고, 네트워크에서 온 것을 읽는 시스템은 Tick 레인에 둔다.
            if (!isFixed && phaseIndex == 0) context.DrainInbox();

            if (isFixed) context.PublishFixedEvents();
            else context.PublishEvents();

            context.Flush();
        }

        private static void Run(Action body)
        {
            try { body(); }
            catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
        }
    }
}
