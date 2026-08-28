using System;
using System.Collections.Generic;

namespace OVFL.ECS
{
    public class Systems
    {
        private readonly Context context;
        private readonly List<ISystem> allSystems = new();
        private readonly List<ISetupSystem> setupSystems = new();
        private readonly List<ITickSystem> tickSystems = new();
        private readonly List<ICleanupSystem> cleanupSystems = new();
        private readonly List<IFixedTickSystem> fixedTickSystems = new();
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
        }

        public virtual Systems AddSystem(ISystem system)
        {
            allSystems.Add(system);

            system.Context = context;

            if (system is ISetupSystem setupSystem)
                setupSystems.Add(setupSystem);

            if (system is ITickSystem tickSystem)
                tickSystems.Add(tickSystem);

            if (system is ICleanupSystem cleanupSystem)
                cleanupSystems.Add(cleanupSystem);

            if (system is IFixedTickSystem fixedTickSystem)
                fixedTickSystems.Add(fixedTickSystem);

            if (system is IFixedCleanupSystem fixedCleanupSystem)
                fixedCleanupSystems.Add(fixedCleanupSystem);

            if (system is ITeardownSystem teardownSystem)
                teardownSystems.Add(teardownSystem);

            return this;
        }

        /// <remarks>
        /// <see cref="AddSystem(ISystem)"/>이 <c>virtual</c>이므로 여기서도 파생 클래스의
        /// 재정의를 탑니다. 둘 중 하나만 가상이면 <b>제네릭으로 넣은 시스템만 파생을 건너뛰는</b>
        /// 어긋남이 생깁니다.
        /// </remarks>
        public virtual Systems AddSystem<T>() where T : ISystem, new()
        {
            var system = new T();
            return AddSystem(system);
        }

        public virtual Systems RemoveSystem(ISystem system)
        {
            allSystems.Remove(system);

            if (system is ISetupSystem setupSystem)
                setupSystems.Remove(setupSystem);

            if (system is ITickSystem tickSystem)
                tickSystems.Remove(tickSystem);

            if (system is ICleanupSystem cleanupSystem)
                cleanupSystems.Remove(cleanupSystem);

            if (system is IFixedTickSystem fixedTickSystem)
                fixedTickSystems.Remove(fixedTickSystem);

            if (system is IFixedCleanupSystem fixedCleanupSystem)
                fixedCleanupSystems.Remove(fixedCleanupSystem);

            if (system is ITeardownSystem teardownSystem)
                teardownSystems.Remove(teardownSystem);

            return this;
        }

        public virtual void RemoveAllSystems()
        {
            allSystems.Clear();
            setupSystems.Clear();
            tickSystems.Clear();
            cleanupSystems.Clear();
            fixedTickSystems.Clear();
            fixedCleanupSystems.Clear();
            teardownSystems.Clear();
        }

        /// <summary>
        /// 모든 Setup System을 실행합니다 (초기화 시 한 번)
        /// </summary>
        public void Setup()
        {
            foreach (var system in setupSystems)
            {
                try { system.Setup(); }
                catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
            }
        }

        /// <summary>
        /// 모든 Tick System을 실행합니다.
        /// </summary>
        public void Tick()
        {
            if (context != null) context.Tick++;

            foreach (var system in tickSystems)
            {
                try { system.Tick(); }
                catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
            }
        }

        /// <summary>
        /// 모든 Cleanup System을 실행합니다. Tick() 이후 직접 호출해야 합니다.
        /// </summary>
        public void Cleanup()
        {
            context?.FlushDestroyQueue();
            try
            {
                foreach (var system in cleanupSystems)
                {
                    try { system.Cleanup(); }
                    catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
                }
            }
            // 예외를 다시 던지더라도 삭제 큐는 비운다. 안 그러면 죽은 엔티티가
            // 다음 스텝까지 남아, 원래 예외와 무관한 곳에서 두 번째 사고가 난다.
            finally { context?.FlushDestroyQueue(); }
        }

        /// <summary>
        /// 모든 FixedTick System을 실행합니다.
        /// </summary>
        public void FixedTick()
        {
            if (context != null) context.FixedTick++;

            foreach (var system in fixedTickSystems)
            {
                try { system.FixedTick(); }
                catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
            }
        }

        /// <summary>
        /// 모든 FixedCleanup System을 실행합니다. FixedTick() 이후 직접 호출해야 합니다.
        /// </summary>
        public void FixedCleanup()
        {
            context?.FlushDestroyQueue();
            try
            {
                foreach (var system in fixedCleanupSystems)
                {
                    try { system.FixedCleanup(); }
                    catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
                }
            }
            finally { context?.FlushDestroyQueue(); }
        }

        /// <summary>
        /// 모든 Teardown System을 실행합니다 (마무리 시 한 번)
        /// 실행 완료 후 모든 시스템이 자동으로 해제됩니다.
        /// </summary>
        public void Teardown()
        {
            try
            {
                foreach (var system in teardownSystems)
                {
                    try { system.Teardown(); }
                    catch (Exception e) { if (RethrowOnSystemException) throw; UnityEngine.Debug.LogException(e); }
                }
            }
            // Teardown이 실패해도 시스템 목록은 반드시 비운다. 남겨두면 이미 정리된
            // 리소스를 붙든 시스템이 다음 Setup에서 되살아난다.
            finally
            {
                context?.FlushDestroyQueue();
                RemoveAllSystems();
            }
        }
    }
}
