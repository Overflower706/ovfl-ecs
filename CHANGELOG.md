# Changelog

All notable changes to this project will be documented in this file.

## [3.4.1] - 2026-08-30

### Added (테스트)
핵심 동작 중 명세로 박혀 있지 않던 것들을 채웠다. 코드는 그대로다.

- **이벤트가 경계를 넘는 경로** — 마지막 Phase(`Outbox`)에서 발행한 것은 그 뒤에 경계가 없어
  스텝 끝의 정리에 걸리지 않고 **다음 스텝의 첫 경계**에서 발행된다. 스텝 밖에서 발행한 것도 같다.
  읽는 도중에 되먹인 것은 다음 스텝이 아니라 **이 스텝의 다음 Phase**에서 살고 스텝 끝에 사라진다.
- **손잡이의 경계** — 두 번 지우면 두 번째는 `false`. 다른 Context의 엔티티는 지워지지 않는다.
  `IsAlive(null)`은 `false`. ID와 세대가 같으면 **다른 Context에서 만든 것이어도 같다** — 손잡이는 값이다.
- **컴포넌트 키는 넘긴 인스턴스의 실제 타입** — 파생 타입으로 넣으면 기반 타입으로는 찾히지 않는다.
  「상속으로 컴포넌트를 묶는다」가 성립하지 않는다는 뜻이다.
- **`AllEntities`는 지연 열거** — 도중에 `Flush`하면 터진다. `GetEntitiesWith`가 목록을 떠서 주는 이유다.
- **등록의 성질** — 한 시스템이 여러 인터페이스를 구현하면 전부에 들어가되 시스템 수는 하나다.
  같은 시스템을 두 Phase에 넣으면 두 번 돈다(막지 않는다). `Teardown` 뒤에는 아무것도 안 돈다.
  `Setup`이 던져도 그 전에 만든 엔티티는 `finally`의 `Flush`로 반영된다.
- **쿼리** — 돌려받은 목록을 고쳐도 Context는 그대로다. 삭제 예약된 것은 빠진다.

**EditMode 143개 전부 통과** (Unity 6000.3.8f1, batchmode). 컴파일 경고 0.

## [3.4.0] - 2026-08-30

### Added
- **`Snapshot.FixedTick`.** 두 레인의 스텝 번호를 다 담는다.
  `Capture`는 어느 레인에서 불렸는지 알 수 없으므로, 하나만 담으면 다른 레인에서 뜬 스냅샷에
  **엉뚱한 번호가 붙는다.** 한 프레임에 FixedUpdate가 세 번 돌면 `Tick`은 셋 다 같아서
  그 셋을 가르는 것은 `FixedTick`뿐이다.
- `Snapshot.ToString()`.

### Changed
- **`Snapshot`의 생성자가 `internal`이다.** 만드는 곳은 `Context.Capture()` 하나뿐이고,
  밖에서 지어 낸 스냅샷은 어느 세계도 가리키지 않아 `Diff`의 결과가 무엇을 뜻하는지 말할 수 없다.
  닫아 두면 이 시그니처가 다시는 파괴적 변경이 되지 않는다.

## [3.3.0] - 2026-08-30

### Added
- **스냅샷 — `ISnapshotable` · `Snapshot` · `Context.Capture()` · `Snapshot.Diff()`.**
  「이 스텝에서 무엇이 어떻게 바뀌었나」를 한 곳에서 볼 수 있게 한다. 디버깅과 테스트를 위한 것이다.

  **되돌리지는 않는다.** 복원은 엔티티를 되살리는 문제로 이어지고, 그때는 컴포넌트가 든
  Unity 오브젝트 참조와 `Entity` 손잡이를 어떻게 되맞출지부터 정해야 한다. 뜨고 비교하는
  것만으로도 쓸 데가 있으므로 거기까지만 한다.

  **값을 내는 것은 오픈인이다.** `IComponent`에 제약이 없어서 패키지는 임의의 구현체를
  어떻게 복사하는지 알 수 없다 — 컴포넌트가 `MonoBehaviour`일 수도 있다.
  무엇이 「상태」인지는 그 컴포넌트만 안다.

  **구현하지 않아도 스냅샷에는 남는다.** `Capture`는 모든 컴포넌트의 있고 없음을 적고,
  값은 `ISnapshotable`인 것만 담는다. 그래서 태그처럼 필드가 없는 것도 붙고 떨어진 것이
  `Diff`에 잡힌다.

  `Diff`의 키는 `(EntityID, Generation, ComponentType)`이다. **세대가 다르면 다른 엔티티**이므로,
  지웠다 같은 ID로 다시 만든 것은 `Removed`와 `Added`로 나뉜다.

### Changed
- 쿼리 절에 `GetUniqueComponent`·`GetUniqueEntityWithComponent`가 `[Obsolete]`임을 적었다.
  README의 인박스 예시도 `TryGetUniqueComponent`로 바꿨다 — 이주하라고 적어 둔 API를
  대표 예시가 쓰고 있었다.

### Added (테스트)
- 스냅샷 16개.
- 빈틈 넷 — `Remove`가 FixedTick 버킷에서도 뺀다 / `Setup`에서 만든 엔티티는 Setup이 끝나면
  쿼리에 잡힌다 / `Enqueue(null)`은 던진다 / 로그를 남기는 옛 쿼리 API 셋.
- **EditMode 121개 전부 통과** (Unity 6000.3.8f1, batchmode).

## [3.2.0] - 2026-08-30

### Changed
- **인박스를 `Tick` 레인이 소유한다.** `Systems.FixedTick`은 더 이상 배출하지 않는다.
  두 레인이 나눠 배출하면 인박스 안에서 `RaiseEvent`(비-fixed)로 낸 것이 어느 큐로 갈지가
  RPC 도착 시점에 따라 갈렸다 — `FixedTick`이 배출하면 그 경계는 `PublishFixedEvents`만
  부르므로 그 이벤트가 다음 `Tick` 경계까지 잠들었다. **넣는 쪽이 피할 방법이 없는 어긋남**이라
  배출 지점을 하나로 모았다.

  대가는 밖에서 온 변경을 `FixedTick`이 **최대 한 프레임 늦게** 본다는 것이다.
  네트워크에서 온 값을 읽는 시스템은 `ITickSystem`에 둔다 —
  `IFixedTickSystem`은 물리처럼 자기 상태로 도는 것을 위한 자리다.
- **`Systems.FixedTick`이 `Boundary()`를 쓴다.** 경계 코드를 두 벌 적어 두어
  한쪽만 고치는 사고가 나던 것을 `Boundary(int phaseIndex, bool isFixed)` 하나로 합쳤다.
  `FixedTick`도 Phase 순서와 이벤트 발행은 그대로다.

### Added
- 테스트 셋 — 인박스는 `FixedTick`이 배출하지 않는다 / `FixedTick`도 Phase 순서로 돈다 /
  `FixedTick`의 이벤트는 fixed 큐로만 간다.

## [3.1.3] - 2026-08-30

### Removed
- **다리(bridge) 설계를 README에서 걷어냈다.** `EcsScene`·`Bind`·`SessionBridge`·`CatBridge`·
  `OwnedByMeComponent`·`NetworkOutboxSystem`은 이 패키지에 없고, 들어올 수도 없다 —
  `Runtime/OVFL.ECS.Runtime.asmdef`의 `references`가 비어 있어 Netcode를 참조하지 않으므로
  `NetworkVariable`·`[Rpc]`를 쓰는 코드는 이 어셈블리에서 컴파일되지 않는다.
  그 설계는 Catverse 위키의 `Docs/OvflEcs.html`이 소유하고, README는 링크만 든다.

### Changed
- 네트워크 절을 **「인박스 — 밖에서 들어온 것을 받는 자리」**로 바꿨다.
  이 패키지가 밖에서 들어온 것에 대해 주는 것은 `Context.Enqueue` 하나다.
- **인박스 배출 지점을 바로잡았다.** 배출은 `Tick()`과 `FixedTick()` 중 **먼저 오는 쪽**의 맨 앞에서
  일어난다. 두 레인이 같은 인박스를 나눠 배출하므로 어느 쪽이 배출할지는 고를 수 없고,
  인박스 안에서 `RaiseEvent`(비-fixed)를 부른 경우 배출이 `FixedTick()`에서 일어났다면
  그 이벤트는 다음 `Tick()` 경계에서 발행된다. `Context.Enqueue`의 주석도 같이 고쳤다.
- 쿼리·이벤트 API 목록에 빠져 있던 것을 채웠다 —
  `GetUniqueEntityWithComponent`·`GetUniqueComponent`·`TryGetEntityByID`·`GetEntityByID`·`ProcessEventsWhere`.

## [3.1.2] - 2026-08-29

### Fixed
- **`3.1.1`의 `package.json`이 빈 파일이었다.** 그 태그로는 Unity가 패키지를 읽지 못한다.
  **`3.1.1`을 쓰지 말고 이것을 쓴다.**

### Changed
- 시스템과 다리가 주고받는 방식을 README에 코드로 적었다.
  다리는 `NetworkObject`마다 하나이며, 오브젝트 다리는 그 엔티티의 컴포넌트로 붙는다.
  게임플레이 시스템은 대상을 실은 이벤트만 발행하고, `Phase.Outbox`의 시스템 하나가 다리를 부른다.
- 인박스의 두 성질을 명세로 박았다 — 인박스에서 발행한 이벤트는 그 스텝에서 읽히고,
  인박스는 넣은 시점이 아니라 **배출 시점의 세계**에서 실행된다.

## [3.1.1] - 2026-08-29

### Changed
- **인박스가 무엇을 보장하는지 정확히 적었다.** 코드 동작은 그대로다.
  「다음 스텝에 적용된다」고 적혀 있었으나 **몇 번째 스텝일지는 정해지지 않는다** —
  배출은 `Systems.Tick()` 맨 앞에서 하므로, 그 프레임의 Tick보다 먼저 도착했으면
  그 프레임에, 늦었으면 다음 프레임에 적용된다. 도착 시점은 네트워크가 정한다.

  보장되는 것은 **적용 지점**이다. 시스템이 도는 도중에는 적용되지 않으므로
  **한 스텝의 모든 시스템이 같은 세계를 본다.** 함께 배출된 것들은 넣은 순서를 지킨다.
- 이 보장을 명세로 박은 테스트를 더했다 — 여섯 Phase가 한 스텝 안에서 같은 값을 본다.

## [3.1.0] - 2026-08-29

### Removed (Breaking)
- **`Entity.IsActive`** — 살아 있는지는 `Context.IsAlive(entity)`가 답한다.
  엔티티에 플래그를 들려 두면 **Context가 아는 것과 엔티티가 아는 것이 어긋날 수 있는
  두 자리**가 생긴다. 삭제 예약 직후가 그랬다 — 플래그는 내려갔는데 세대는 그대로여서,
  조회하는 곳마다 둘을 함께 확인해야 했다.

### Changed
- **세대를 `DestroyEntity` 시점에 올린다.** 검사 하나가
  쿼리에서 빼는 것 · 두 번 지우는 것을 막는 것 · 낡은 손잡이를 걸러내는 것을 함께 한다.
  세대는 ID가 재사용된 뒤에도 낡은 손잡이를 구분하므로, 플래그가 하던 일을 전부 덮는다.
- `Entity`에 가변 상태가 없다. `ID` · `Generation` · 컴포넌트뿐이다.

## [3.0.1] - 2026-08-29

### Changed
- 주석과 README에서 과거 서술을 걷어냈다. 코드 동작은 그대로다.
  **`3.0.0` 대신 이것을 쓰면 된다.**

## [3.0.0] - 2026-08-29

**파괴적 변경이 있다.** 옮기는 법은 아래 「이주」와 `README.md`를 보라.

### Added
- **`Phase`** — 시스템의 실행 순서를 타입이 갖는다.
  `Inbox` → `Input` → `Simulation` → `Reaction` → `View` → `Outbox`.
  이전에는 <b>등록한 줄 순서</b>가 곧 실행 순서라, 한 줄을 옮기면 동작이 바뀌는데
  컴파일러도 테스트도 몰랐다.
- **`Systems.Add(Phase, ISystem)` / `Add<T>(Phase)`** — 새 등록 API.
- **`Context.Enqueue(Action<Context>)`** — 밖에서 들어온 변경을 스텝의 정해진 지점까지 미룬다.
  **네트워크 RPC를 안전하게 받는 자리다.** `SendTo.ClientsAndHost` RPC는 호스트에서 즉시
  실행되므로, 그대로 두면 어떤 시스템이 반쯤 돌던 중에 Context가 바뀐다.
- `Context.InboxCount` / `PendingCount` / `PendingEventCount` — 상태를 들여다보는 창.
- **`com.ovfl.ecs.extensions`를 흡수했다** — 이벤트와 쿼리가 이 패키지 안으로 들어왔다.

### Changed (Breaking)
- **`CreateEntity`가 즉시 등장시키지 않는다.** 만든 엔티티는 <b>다음 Phase 경계</b>부터
  쿼리에 잡힌다. 컴포넌트를 붙이고 값을 읽는 것은 즉시 되고 `IsAlive`도 true다 —
  미뤄지는 것은 **쿼리에 잡히는 시점**뿐이다.
  덕분에 `foreach (var e in ctx.AllEntities) ctx.CreateEntity();`가 더 이상 터지지 않는다.
- **이벤트 발행·정리가 `Systems` 안으로 들어갔다.** `EventPublisherSystem`,
  `EventCleanupSystem`, `FixedEventPublisherSystem`, `FixedEventCleanupSystem`을 **삭제했다.**
  등록하지 않는다. 「맨 앞에 등록」·「맨 뒤에 등록」이라는 <b>주석으로만 있던 규약</b>이 사라졌다.
- **이벤트는 발행한 Phase에서는 보이지 않는다.** 다음 Phase 경계에서 발행된다.
  같은 Phase 안의 등록 순서가 이벤트를 통해서는 결과를 바꾸지 못하게 하기 위해서다.
- **`EventMetadataComponent.CreatedTime`(`float`)이 `CreatedTick`(`uint`)으로.**
  프레임 시간이 아니라 스텝 번호다.
- 이벤트 큐가 `Context` 안으로 들어왔다. 패키지가 갈려 있던 탓에 쓰던
  `ConditionalWeakTable` 우회가 사라졌고, **대기 중인 이벤트가 Context에서 보인다.**

### Deprecated
- `AddSystem(ISystem)` / `AddSystem<T>()` / `RemoveSystem(ISystem)` —
  경고만 내고 동작한다. `AddSystem`은 `Phase.Simulation`으로 들어간다.
  **이주용이며 다음 메이저에서 지운다.**

### 이주
1. `manifest.json`에서 `com.ovfl.ecs.extensions`를 **지운다.**
   두면 타입 중복으로 컴파일이 깨진다.
2. `EventPublisherSystem` 계열 네 개의 등록 줄을 **지운다.**
3. `AddSystem(x)`를 `Add(Phase.___, x)`로 옮긴다. 안 옮겨도 컴파일은 되지만
   그동안은 전부 `Simulation`에 모여 예전과 같은 「등록 순서」로 돈다.
4. `CreatedTime`을 쓰던 곳을 `CreatedTick`으로 바꾼다.

## [2.1.0] - 2026-08-29

파괴적 변경 없음. 소비자는 태그만 올리면 된다.

### Added
- **`Context.Tick` / `Context.FixedTick`** — `Systems.Tick()` / `FixedTick()`이 돈 횟수.
  시스템이 도는 동안 이미 증가해 있으므로 첫 Tick 안에서 읽으면 1이다.
  `Time.time`과 달리 한 스텝 안에서 값이 고정이라, 같은 스텝에 생긴 것들을 묶어 볼 수 있다.
- **`Systems.RethrowOnSystemException`** — 시스템이 던진 예외를 호출자에게 다시 던질지.
  **에디터 기본값 `true`**, 빌드 기본값 `false`.
  삼키면 게임은 버티지만 죽은 시스템이 갱신하지 못한 값을 뒤 시스템이 읽어
  **증상이 원인에서 멀어진다.** 개발 중에는 첫 예외에서 멈추는 편이 낫다.

### Fixed
- **`Entity.AddComponent`가 정적 타입으로 키를 잡던 문제** — `component.GetType()`으로 바꿨다.
  `IComponent c = new Foo(); e.AddComponent(c);`처럼 기반 타입 변수로 넘기면
  키가 `IComponent`에 박혀 `GetComponent<Foo>()`가 **경고도 없이 null을 주던** 상태였다.
  기반 타입 키로 저장하는 것에 의존하던 코드가 있다면 동작이 바뀐다.
- **`Systems.AddSystem`이 `virtual`이 아니던 문제** — `AddSystem(ISystem)` / `AddSystem<T>()` /
  `RemoveSystem` / `RemoveAllSystems`를 `virtual`로 바꿨다.
  파생 클래스가 `new`로 숨기면 **제네릭으로 넣은 시스템만 파생 목록을 건너뛰는** 어긋남이 있었다.
- **`Context.GetEntity`가 삭제 예약된 엔티티를 돌려주던 문제** — `IsActive`가 false면 null을 준다.
  `AllEntities`에서는 이미 빠져 있는데 ID로는 잡혀서, 둘의 답이 갈렸다.
- **예외를 다시 던질 때 뒤처리를 건너뛰던 문제** — `Cleanup` / `FixedCleanup` / `Teardown`의
  마무리(`FlushDestroyQueue`, `RemoveAllSystems`)를 `finally`로 옮겼다.
- **`Entity.AddComponent(null)`이 조용히 통과하던 문제** — `ArgumentNullException`을 던진다.

## [2.0.2] - 2026-04-28

### Changed
- OVFL.ECS 테스트 프로젝트에 로컬 패키지로 연결

## [2.0.0] - 2026-04-08

### Removed (Breaking Changes)
- **이벤트 시스템 전체 제거** — v1.7.0~v1.9.0에서 추가된 이벤트 시스템을 패키지에서 분리. 게임 프로젝트가 각자 이벤트 레이어를 구현하는 방향으로 전환.
  - `EventComponent` 제거
  - `EventMetadataComponent` 제거
  - `EventPublisherSystem` / `FixedEventPublisherSystem` 제거
  - `EventCleanupSystem` / `FixedEventCleanupSystem` 제거
  - `EventExtensions` 제거
  - `Context.RaiseEvent<T>()` 제거
  - `Context.RaiseFixedEvent<T>()` 제거
  - `Context.ProcessEvents<T>()` / `ProcessEventsWhere<T>()` 제거
  - `EventSystemTests` 제거

### Changed
- **Assembly Definition 이름 변경**: `OVFL.ECS` → `OVFL.ECS.Runtime`
  - 테스트 asmdef의 references도 `OVFL.ECS.Runtime`으로 갱신

## [1.9.2] - 2026-04-07

### Fixed
- **`Entity.operator !=` null 안전성 버그 수정** — `left`가 null일 때 `NullReferenceException` 발생하던 문제 수정
  ```csharp
  // 수정 전: !left.Equals(right) → left가 null이면 크래시
  // 수정 후: !(left == right)
  ```
- **`Systems.Teardown()` 이후 `FlushDestroyQueue` 추가** — Teardown 중 `DestroyEntity()` 호출 시 flush되지 않던 문제 수정

### Changed
- `Systems.context` 필드에 `readonly` 추가

### Tests
- `EntityComponentTests`에 `operator ==`, `!=` null 안전성 테스트 추가

## [1.9.1] - 2026-04-07

### Added
- **`Context.EntityCount` 프로퍼티 추가** — 현재 활성 Entity 수 반환
  ```csharp
  int count = context.EntityCount;
  ```

- **`Context.DestroyAllEntities()` 메서드 추가** — 모든 Entity를 일괄 삭제 예약
  ```csharp
  context.DestroyAllEntities();
  context.FlushDestroyQueue(); // 또는 systems.Cleanup() 자동 처리
  ```

### Tests
- `ContextTests`에 EntityCount / DestroyAllEntities 테스트 추가

## [1.9.0] - 2026-04-07

### Added
- **FixedEvent 시스템 추가** — `RaiseFixedEvent<T>()`, `FixedEventPublisherSystem`, `FixedEventCleanupSystem`
  ```csharp
  // FixedUpdate 주기 이벤트 발행
  context.RaiseFixedEvent(new MyEvent());
  
  // 시스템 등록 순서
  systems.AddSystem(new FixedEventPublisherSystem()); // FixedTick 목록 맨 앞
  systems.AddSystem(new FixedEventCleanupSystem());   // FixedCleanup 목록 맨 뒤
  ```
  - Update 이벤트와 FixedUpdate 이벤트가 독립적으로 관리됨
  - `EventCleanupSystem`은 일반 이벤트만, `FixedEventCleanupSystem`은 FixedEvent만 정리

### Changed
- **`EventMetadataComponent.IsFixed` 추가** — Event Entity가 Update/FixedUpdate 중 어느 주기로 발행됐는지 구분
- **`EventCleanupSystem`** — `IsFixed=false`인 이벤트 Entity만 정리하도록 변경 (FixedEvent와 격리)

### Tests
- `EventSystemTests` 추가 — 이벤트 시스템 전체 커버리지
  - `RaiseEvent` / `RaiseFixedEvent` Publish 전/후 동작 검증
  - `ProcessEvents` / `ProcessEventsWhere` 필터링 검증
  - Cleanup 격리 검증 (일반 Event ↔ FixedEvent 서로 건드리지 않음)
  - `EventMetadataComponent.IsFixed` 플래그 검증

## [1.8.0] - 2026-04-06

### Changed (Breaking)
- **`EventQueueComponent` 제거** — 이벤트 예약 API가 `Context`로 이동됨
  ```csharp
  // Before
  eventQueueEntity.GetComponent<EventQueueComponent>().Enqueue(new MyEvent());

  // After
  context.RaiseEvent(new MyEvent());
  ```
  - `EventPublisherSystem`이 `EventQueueComponent` 엔티티 탐색 대신 `Context` 내부 큐를 직접 사용
  - `EventExtensions.CreateEvent` — `internal`로 변경 (구현 세부사항)

## [1.7.3] - 2026-04-06

### Changed (Breaking)
- **`UnregisterSystem()` → `RemoveSystem()`** — `AddSystem`과 대칭되는 이름으로 변경
- **`UnregisterAll()` → `RemoveAllSystems()`** — 일관성 유지

## [1.7.2] - 2026-04-06

### Changed
- **Systems 예외 격리** — 한 System에서 예외가 발생해도 이후 System들이 계속 실행됨
  - 기존: 예외 발생 시 해당 프레임의 나머지 System이 모두 스킵됨
  - 변경: 각 System 호출을 try-catch로 감싸 예외를 `Debug.LogException`으로 기록 후 계속 진행
  - `Setup`, `Tick`, `Cleanup`, `FixedTick`, `FixedCleanup`, `Teardown` 모든 라이프사이클에 적용

### Tests
- `Setup_WhenOneSystemThrows_OtherSystemsShouldStillRun` 추가
- `Tick_WhenOneSystemThrows_OtherSystemsShouldStillRun` 추가
- `Cleanup_WhenOneSystemThrows_OtherSystemsShouldStillRun` 추가
- `Teardown_WhenOneSystemThrows_OtherSystemsShouldStillRun` 추가

## [1.7.0] - 2026-04-06

### Added
- **이벤트 시스템 추가** — `EventComponent`, `EventQueueComponent`, `EventMetadataComponent`, `EventPublisherSystem`, `EventCleanupSystem`, `EventExtensions`가 `OVFL.ECS` 패키지에 편입됨
  ```csharp
  // 이벤트 정의
  public class MyEvent : EventComponent { }

  // 이벤트 발행
  queue.Enqueue(new MyEvent());

  // 이벤트 처리
  Context.ProcessEvents<MyEvent>((entity, e) => { ... });
  ```
  - `EventPublisherSystem`을 시스템 목록 **맨 앞**, `EventCleanupSystem`을 **맨 뒤**에 등록
  - 현 프레임에서 Enqueue된 이벤트는 다음 프레임에 발행됨 (순환 이벤트 체인 방지)
- **`IFixedCleanupSystem` 인터페이스 추가** — FixedTick 주기의 정리 작업을 위한 전용 인터페이스
  ```csharp
  public class MyFixedCleanupSystem : IFixedCleanupSystem
  {
      public Context Context { get; set; }
      public void FixedCleanup() { ... }
  }
  ```

### Changed
- **`Systems.Tick()` — Cleanup 실행 보장** — Tick 도중 예외가 발생해도 Cleanup이 반드시 실행됨 (try/finally 내재화)
- **`Systems.FixedTick()` — FixedCleanup 실행 보장** — FixedTick 도중 예외가 발생해도 FixedCleanup이 반드시 실행됨
  - 기존: ECSRunner에서 try/finally로 직접 보장해야 했음
  - 변경: `Systems`가 내부적으로 보장하므로 ECSRunner 구현 단순화 가능
    ```csharp
    // Before: ECSRunner에서 try/finally 필요
    private void Update()
    {
        try { systems.Tick(); }
        finally { systems.Cleanup(); }
    }

    // After: 단순 호출로 충분
    private void Update() { systems.Tick(); }
    ```

## [1.6.0] - 2026-04-06

### Removed (Breaking Changes)
- **Systems() 기본 생성자 제거** — `Systems(Context context)` 생성자를 사용하세요.
  ```csharp
  // Before (더 이상 동작하지 않음)
  var systems = new Systems();
  systems.SetContext(context);
  
  // After
  var systems = new Systems(context);
  ```
- **Systems.SetContext() 제거** — 생성자로 Context를 전달하세요.
- **Context.GetEntities() 제거** — `Context.AllEntities`를 사용하세요.
  ```csharp
  // Before (더 이상 동작하지 않음)
  var entities = context.GetEntities();
  
  // After
  var entities = context.AllEntities;
  ```
- **Context.GetEntitiesWithComponent\<T\>() 제거** — `AllEntities`를 직접 순회하세요.
  ```csharp
  // Before (더 이상 동작하지 않음)
  var players = context.GetEntitiesWithComponent<PlayerComponent>();
  
  // After
  var players = context.AllEntities.Where(e => e.HasComponent<PlayerComponent>());
  ```

## [1.5.6] - 2026-04-06

### Fixed
- **Entity.AddComponent&lt;T&gt;(T component) 타입 키 불일치 수정** — 런타임 타입(`component.GetType()`) 대신 컴파일 타임 타입(`typeof(T)`)으로 저장하도록 변경. `GetComponent`, `HasComponent`, `RemoveComponent`와 일관성 유지.
- **Entity.Null.IsActive 의미 불일치 수정** — `Entity.Null` 생성 시 `IsActive = false`로 초기화. null 객체가 활성 상태로 표시되던 논리적 모순 해결.

### Tests
- `TryGetComponent` 성공/실패 케이스 추가
- `AddComponent` 같은 타입 재등록(덮어쓰기) 케이스 추가
- `Entity.Null` — `IsNull`, `IsActive` 케이스 추가
- `Context.GetEntity` 잘못된 ID → null 반환 케이스 추가
- `ICleanupSystem` 라이프사이클 케이스 추가
- `IFixedTickSystem` 라이프사이클 케이스 추가

## [1.5.5] - 2026-04-06

### Added
- **Context.FlushDestroyQueue()** 메서드 추가 — 삭제 큐에 쌓인 엔티티를 즉시 일괄 제거
  ```csharp
  context.FlushDestroyQueue(); // Systems 없이 직접 사용 시 수동 호출
  ```

### Changed
- **DestroyEntity() 지연 삭제로 변경** — 호출 즉시 삭제하지 않고 큐에 등록 후 `Tick()` / `FixedTick()` 완료 시 자동 처리
  ```csharp
  // 이제 Tick 내에서 안전하게 호출 가능
  foreach (var entity in Context.AllEntities)
  {
      if (isDead) Context.DestroyEntity(entity); // 예외 없음
  }
  ```
- **AllEntities 필터링** — `IsActive=false`인 엔티티(삭제 예약된 엔티티) 제외
- **IsAlive() 조기 반환** — `IsActive=false`이면 즉시 false 반환
- **Systems.Tick() / Systems.FixedTick()** — 모든 시스템 실행 후 `FlushDestroyQueue()` 자동 호출

## [1.5.4] - 2026-03-10

### Added
- **Systems.UnregisterSystem()** 메서드 추가 — 특정 시스템을 모든 라이프사이클 리스트에서 제거
  ```csharp
  systems.UnregisterSystem(mySystem);
  ```
- **Systems.UnregisterAll()** 메서드 추가 — 등록된 모든 시스템을 일괄 해제
  ```csharp
  systems.UnregisterAll();
  ```
- **Teardown() 자동 Unregister**: `Teardown()` 실행 완료 후 `UnregisterAll()`이 자동으로 호출되어 모든 시스템이 해제됨

## [1.5.0] - 2026-01-30

### Changed
- 0번째 Entity 또는 별도의 null Entity 처리 추가
- Entity Pooling 기능 추가
- Try Get Component를 추가
- Systems에 Context를 받는 생성자가 추가됐습니다. 이제 SetContext 대신 Systems 생성자를 사용해야합니다.
- Reactive 기능 제거

## [1.3.0] - 2025-08-08

### Changed
- **Entity.AddComponent() 세부 구현 변경**: 런타임 타입 기반으로 추적됨
  ```csharp
  // Before - 복잡한 리플렉션 코드 필요
  var methods = typeof(Entity).GetMethods(BindingFlags.Public | BindingFlags.Instance);
  // ...복잡한 리플렉션 로직
  
  // After - 간단한 비제네릭 메서드 사용
  entity.AddComponent(notifyComponent); // IComponent 타입으로 직접 추가
  ```
- **컴포넌트 쿼리 성능 대폭 개선**: 선형 검색에서 해시 기반 캐싱으로 변경
  ```csharp
  // 기존: O(n) - 매번 모든 엔티티 순회
  // 개선: O(1) - 컴포넌트 타입별 엔티티 캐시 활용
  var entities = context.GetEntitiesWithComponent<PlayerComponent>(); // 즉시 반환
  ```
- **Context 클래스 캐싱 시스템**: 컴포넌트 추가/제거 시 자동 캐시 업데이트
  - `Dictionary<Type, HashSet<Entity>>` 기반 캐시 구조
  - Entity의 컴포넌트 변경 이벤트(`OnComponentAdded`, `OnComponentRemoved`)와 연동
  - 엔티티 제거 시 모든 관련 캐시에서 자동 정리
- **시스템 확장성 향상**: 시스템 수 증가 시에도 성능 저하 없음
  - 매 프레임 컴포넌트 검색 비용을 O(시스템 수 × 엔티티 수)에서 O(시스템 수)로 감소
  - NotifySystem 등에서 복잡한 리플렉션 코드 제거로 가독성 및 유지보수성 향상

### Performance Improvements
- **GetEntitiesWithComponent<T>()** 메서드 성능 최적화
- 대용량 엔티티 환경에서 쿼리 성능 대폭 향상 (1000개 엔티티 기준 100회 조회 시 100ms 이내)
- 메모리 사용량 최적화: 중복 검색 제거로 CPU 캐시 효율성 증대

## [1.2.0] - 2025-07-14

### Changed
- **Systems.Add()** → **Systems.AddSystem()** 메서드명 변경 (명확성 향상)
- **Systems.SetContext()** 메서드 추가로 Context 자동 할당 지원
- **ISystem 인터페이스 메서드 시그니처 변경**
  ```csharp
  // Before
  void Setup(Context context);
  void Tick(Context context);
  void Cleanup(Context context);
  void Teardown(Context context);
  
  // After
  void Setup();
  void Tick();
  void Cleanup();
  void Teardown();
  ```
- **ISystem.Context 속성** 추가
  ```csharp
  // 모든 시스템이 Context 속성을 가지며, 시스템 등록 시 자동으로 할당됨
  public class MySystem : ITickSystem
  {
      public Context Context { get; set; }  // 자동 할당
      
      public void Tick()  // Context 파라미터 제거
      {
          // this.Context 사용
          var entities = this.Context.GetEntities();
      }
  }
  ```
- 타입 안정성 향상
- 코드 간결성 개선 (Context 파라미터 제거로 메서드 시그니처 단순화)
- 시스템 등록 시 Context 자동 할당으로 사용성 향상

## [1.1.0] - 2025-07-09

### Added
- **AddComponent<T>()** 제네릭 메서드 추가
  ```csharp
  // Before
  entity.AddComponent(new GridComponent());
  
  // After
  entity.AddComponent<GridComponent>();
  ```
- **GetEntitiesWithComponent<T>()** 메서드 추가
  ```csharp
  // Context에서 특정 Component를 가진 Entity들 조회 (성능 최적화된 for문 사용)
  var gameEntities = context.GetEntitiesWithComponent<GameStateComponent>();
  ```
- **Systems.AddSystem<T>()** 제네릭 메서드 추가
  ```csharp
  // Before
  Systems.AddSystem(new DataSystem());
  
  // After
  Systems.AddSystem<DataSystem>();
  ```

## [1.0.0] - 2025-07-03

### Added
- 초기 ECS 구현체 릴리즈
- **IComponent 인터페이스**: 순수한 데이터 컴포넌트 정의
- **Entity 클래스**: 컴포넌트 관리 (추가, 제거, 조회, 존재 확인)
- **Context 클래스**: 엔티티 생성 및 관리
- **Systems 클래스**: 시스템 등록 및 라이프사이클 관리
- **시스템 라이프사이클 인터페이스들**:
  - `ISetupSystem`: 초기화 시 한 번 실행
  - `ITickSystem`: 매 프레임 실행
  - `ICleanupSystem`: Tick 이후 정리 작업
  - `ITeardownSystem`: 마무리 시 한 번 실행
- **포괄적인 Unit Test 스위트**: 모든 핵심 기능에 대한 테스트
- **통합 테스트**: 실제 사용 시나리오 검증
- **Unity Package Manager 지원**: Git URL을 통한 패키지 설치

### Features
- **순수한 C# 구현**: Unity 의존성 최소화로 재사용성 극대화
- **체이닝 메서드 지원**: `systems.AddSystem(sys1).AddSystem(sys2)` 형태로 편리한 사용
- **타입 안전성 보장**: 제네릭을 활용한 컴파일 타임 타입 검사
- **학습 친화적인 구조**: 복잡한 최적화 없이 ECS 개념에 집중
- **메모리 효율적**: Dictionary 기반 컴포넌트 저장
- **확장 가능한 아키텍처**: 기본 구조 유지하면서 기능 추가 가능

### Technical Details
- Unity 2020.3 이상 지원
- .NET Standard 2.1 호환
- NUnit 기반 테스트 프레임워크
- Edit Mode 테스트 지원
- Assembly Definition 파일 포함
