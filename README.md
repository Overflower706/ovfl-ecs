# OVFL ECS

Unity용 ECS. **구조를 위한 것**입니다.

메모리 최적화(Burst, NativeArray, Job System)는 추구하지 않습니다. 대신
데이터와 기능의 분리, 실행 순서의 명시, 생명주기 관리 같은 **관리 기능**은 챙깁니다.
`Entity`가 class이고 `IComponent`에 struct 강제가 없는 것은 그 선택의 결과입니다 —
「Context가 Entity 배열을 갖고 System이 그것을 순회한다」가 코드에 그대로 보이는 편이,
빠른 편보다 낫다고 봤습니다.

- Unity 6000.1 이상
- 설치: `Packages/manifest.json`에 아래 한 줄. **버전은 태그로 고정하세요.**

```json
"com.ovfl.ecs": "https://github.com/Overflower706/ovfl-ecs.git#3.4.0"
```

> **`com.ovfl.ecs.extensions`는 이 패키지에 흡수됐습니다.**
> manifest에 그 줄이 남아 있으면 타입 중복으로 컴파일이 깨집니다. 지우세요.

---

## 30초 요약

```csharp
// 1. 데이터
public class HealthComponent : IComponent { public int Value; }

// 2. 기능
public class PoisonSystem : ITickSystem
{
    public Context Context { get; set; }          // 등록할 때 자동으로 꽂힙니다

    public void Tick()
    {
        foreach (var entity in Context.GetEntitiesWith<HealthComponent>())
            entity.GetComponent<HealthComponent>().Value -= 1;
    }
}

// 3. 조립
var context = new Context();
var systems = new Systems(context);
systems.Add(Phase.Simulation, new PoisonSystem());

systems.Setup();   // 한 번
systems.Tick();    // 매 프레임
```

---

## 한 스텝에 무슨 일이 일어나나

`Systems.Tick()` 하나가 이렇게 돕니다.

```
Tick++
Phase마다:
    ── 경계 ──  인박스 배출(첫 Phase에서만) · 이벤트 발행 · 생성/삭제 반영
    그 Phase의 시스템들을 등록 된 순서로 실행
마지막:      반영 · 이번 스텝 이벤트 정리 · 반영
```

**경계가 Phase 앞에 있다**는 것이 이 패키지의 중심 규칙입니다. 그래서:

- 앞 Phase가 만든 엔티티는 **뒤 Phase에서 보입니다.**
- 같은 Phase 안에서는 **엔티티 집합이 고정**입니다. 돌면서 만들어도 터지지 않습니다.
- 발행한 이벤트는 **다음 Phase부터** 읽힙니다.

### Phase

| Phase | 무엇을 |
|---|---|
| `Inbox` | 밖에서 들어온 변경을 Context에 넣습니다 (RPC·콜백·비동기 완료) |
| `Input` | 이번 스텝의 입력을 읽어 컴포넌트에 적습니다 |
| `Simulation` | 게임 규칙. **상태를 바꾸는 것은 여기서** |
| `Reaction` | 시뮬레이션 결과에 대한 반응. 이벤트를 주로 여기서 읽습니다 |
| `View` | 화면·사운드에 반영합니다. **여기서 상태를 바꾸지 않습니다** |
| `Outbox` | 밖으로 내보냅니다. RPC 송신·저장 |

**같은 Phase 안의 순서는 등록 순서입니다.** Phase는 큰 덩어리의 순서를 정하고,
그 안에서는 적은 순서대로 돕니다.
한 Phase 안에서 A가 B보다 먼저여야 한다면, 그건 **둘을 다른 Phase로 가르라는 신호**입니다.

---

## Runner — MonoBehaviour와 만나는 자리

ECS는 Unity의 생명주기를 모릅니다. 그 둘을 잇는 MonoBehaviour 하나를 **Runner**라 부릅니다.
**씬마다 하나**를 두는 것을 권합니다.

```csharp
public class IngameECSRunner : MonoBehaviour
{
    private Context context;
    private Systems systems;

    private void Awake()
    {
        context = new Context();
        systems = new Systems(context);

        // Inbox — 밖에서 들어온 것을 넣는 시스템
        systems.Add(Phase.Inbox, new NetworkReceiveSystem());

        // Input
        systems.Add(Phase.Input, new PlayerInputSystem());

        // Simulation
        systems.Add(Phase.Simulation, new MovementSystem());
        systems.Add(Phase.Simulation, new CollisionSystem());

        // Reaction
        systems.Add(Phase.Reaction, new DamageSystem());

        // View
        systems.Add(Phase.View, new AnimationSystem());
    }

    private void Start()      => systems.Setup();
    private void Update()     { systems.Tick();      systems.Cleanup(); }
    private void FixedUpdate(){ systems.FixedTick(); systems.FixedCleanup(); }
    private void OnDestroy()  => systems.Teardown();
}
```

**Runner가 하는 일은 조립과 호출뿐입니다.** 게임 로직을 Runner에 적기 시작하면
ECS 바깥에 상태가 생기고, 그 상태는 Phase 순서 밖에서 바뀝니다.

### Setup / Tick / Cleanup / Teardown

| 인터페이스 | 언제 | Unity 대응 |
|---|---|---|
| `ISetupSystem` | `Setup()` — 한 번 | `Start()` |
| `ITickSystem` | `Tick()` — 매 프레임, **Phase 순서로** | `Update()` |
| `ICleanupSystem` | `Cleanup()` — `Tick()` 직후 | `Update()` 끝 |
| `IFixedTickSystem` | `FixedTick()` — **Phase 순서로** | `FixedUpdate()` |
| `IFixedCleanupSystem` | `FixedCleanup()` | `FixedUpdate()` 끝 |
| `ITeardownSystem` | `Teardown()` — 한 번. 끝나면 시스템 목록이 비워집니다 | `OnDestroy()` |

한 클래스가 여러 개를 구현해도 됩니다. `Phase`는 `Tick`/`FixedTick`에만 적용됩니다 —
나머지는 스텝 전체에 한 번씩 도는 것이라 순서를 나눌 자리가 없습니다.

---

## 인박스 — 밖에서 들어온 것을 받는 자리

`Context.Enqueue` 하나가 이 패키지가 네트워크·UI 콜백·비동기 완료에 대해 주는 전부입니다.
**여기를 틀리면 재현 안 되는 버그가 생깁니다.**

### 문제: RPC는 밖에서 아무 때나 불린다

```csharp
[Rpc(SendTo.ClientsAndHost)]
void ScoreChangedRpc(int value)
{
    context.TryGetUniqueComponent<ScoreComponent>(out var s);
    s.Value = value;                                              // ❌
}
```

이게 왜 위험하냐면, **호스트에서는 이 RPC가 그 자리에서 즉시 실행**되기 때문입니다.
보내는 코드가 `Simulation`의 세 번째 시스템 안이었다면, **그 시점에 Context가 바뀝니다.**
같은 스텝에서 앞 시스템과 뒤 시스템이 서로 다른 세계를 보게 되고,
「어느 시스템이 반쯤 돌던 중이었나」에 따라 결과가 달라집니다. 로그를 봐도 알 수 없습니다.

### 답: 인박스에 넣는다

```csharp
[Rpc(SendTo.ClientsAndHost)]
void ScoreChangedRpc(int value)
{
    context.Enqueue(ctx =>                                        // ✅
    {
        if (ctx.TryGetUniqueComponent<ScoreComponent>(out var s)) s.Value = value;
    });
}
```

이렇게 하면 그 변경이 **가장 가까운 배출 지점**(`Phase.Inbox` 직전)에서,
다른 모든 인박스 항목과 함께, 넣은 순서대로 적용됩니다.

#### 무엇이 보장되고 무엇이 안 되나

**보장되는 것**

- **시스템이 도는 도중에는 적용되지 않습니다.** 한 스텝 안의 모든 시스템은 같은 세계를 봅니다.
- **함께 배출된 것들은 넣은 순서대로** 적용됩니다.
- **어느 스텝에서 적용됐는지 `Context.Tick`으로 확인할 수 있습니다.**

**보장되지 않는 것**

- **몇 번째 스텝에 적용될지는 정해지지 않습니다.** 배출은 `Tick()` 맨 앞에서 일어나므로,
  그 프레임의 `Tick()`보다 먼저 도착했으면 **그 프레임**에, 늦게 도착했으면 다음 프레임에 적용됩니다.

  | 도착한 때 | 적용되는 스텝 |
  |---|---|
  | 그 프레임 `Tick()` 전 (원격에서 온 것은 대개 여기) | 그 프레임 |
  | `Tick()` 도중 (호스트가 자기 RPC를 그 자리에서 실행) | 다음 프레임 |
  | `Tick()` 뒤 | 다음 프레임 |

  **인박스는 `Tick` 레인이 소유합니다.** `FixedTick()`은 배출하지 않으므로
  밖에서 온 변경을 **최대 한 프레임 늦게** 봅니다. 네트워크에서 온 값을 읽는 시스템은
  `ITickSystem`에 두세요 — `IFixedTickSystem`은 물리처럼 자기 상태로 도는 것을 위한 자리입니다.

**언제 도착할지는 네트워크가 정하는 것이라 어떤 설계로도 고정할 수 없습니다.**
인박스가 바꾸는 것은 지연이 아니라 **적용 지점**입니다 — 「아무 데서나」가 「경계에서만」이 됩니다.
그래서 같은 입력에 대해 **시스템들이 보는 세계가 스텝마다 일관되고**, 로그의 `Tick`으로
무엇이 언제 들어왔는지 되짚을 수 있습니다.

> 배출 도중에 새로 들어온 것은 **그 다음 배출**로 넘어갑니다.
> 매 프레임 도착하는 RPC가 스텝을 영영 끝내지 못하게 만들지 않기 위해서입니다.

> **`NetworkBehaviour`와 짝지어 쓰는 설계 — 다리(bridge) 패턴 — 는 이 패키지에 없습니다.**
> `Runtime/OVFL.ECS.Runtime.asmdef`의 `references`가 비어 있어 Netcode를 참조하지 않으므로,
> `NetworkVariable`·`[Rpc]`를 쓰는 코드는 이 어셈블리 안에서 컴파일되지 않습니다.
> 세션 다리·오브젝트 다리, `Phase.Outbox`로 내보내는 형태, 스폰된 다리가 Context를 끌어오는 방법은
> [Catverse 위키의 `Docs/OvflEcs.html` — 「다리(bridge) 설계」](https://github.com/Overflower706/Catverse)가 소유합니다.

---

## 이벤트

시스템 사이의 단방향 통신입니다. **발행·정리는 `Systems`가 알아서 합니다** —
등록할 시스템이 없습니다.

```csharp
public class DamageEvent : EventComponent { public int Amount; }

// 발행 — 어디서든
Context.RaiseEvent(new DamageEvent { Amount = 10 });

// 수신 — 다음 Phase부터
Context.ProcessEvents<DamageEvent>((entity, e) => hp.Value -= e.Amount);
```

- **발행한 Phase에서는 보이지 않습니다.** 다음 Phase 경계에서 발행됩니다.
  그래서 같은 Phase 안의 등록 순서가 이벤트를 통해서는 결과를 바꾸지 못합니다.
- **그 스텝 끝에 사라집니다.** 남겨두면 다음 스텝이 지난 이벤트를 또 읽습니다.
- `FixedTick` 주기는 `RaiseFixedEvent`. 두 레인은 서로의 이벤트를 건드리지 않습니다.
- 여러 시스템이 같은 이벤트를 읽어도 됩니다.
- 걸러 읽으려면 `ProcessEventsWhere<T>(predicate, action)`.

---

## 쿼리

```csharp
Context.GetEntitiesWith<T>()                     // 목록 (스냅샷)
Context.TryGetUniqueEntity<T>(out var entity)    // 정확히 하나일 때 true. 조용합니다
Context.TryGetUniqueComponent<T>(out var comp)
Context.GetEntity(id)                            // Context 본체
Context.TryGetEntityByID(id, out var entity)     // 확장. 조용합니다
Context.GetEntityByID(id)                        // 없으면 에러 로그
Context.AllEntities                              // 지연 열거
```

`GetEntitiesWith`는 **부르는 시점의 목록을 떠서** 줍니다. 결과를 돌면서 엔티티를
만들거나 지워도 안전합니다.

`GetUniqueComponent<T>()`와 `GetUniqueEntityWithComponent<T>()`는 **`[Obsolete]`입니다.**
없거나 여럿일 때 에러 로그를 남기는 옛 API이고, `Try` 쪽으로 옮긴 뒤 다음 메이저에서 지웁니다.

`Try`로 시작하는 것은 **로그를 남기지 않습니다.** 「없을 수도 있다」가 정상인 자리에서
부르면 매 프레임 로그가 쌓이기 때문입니다. 없는 것이 잘못인지는 부른 쪽이 압니다.

---

## 미뤄지는 것과 안 미뤄지는 것

| | 언제 반영되나 |
|---|---|
| 컴포넌트 값 쓰기 | **즉시** |
| 살아 있는 엔티티에 `AddComponent` / `RemoveComponent` | **즉시** |
| `CreateEntity` | 다음 Phase 경계 |
| `DestroyEntity` | 쿼리에서는 **즉시** 사라지고, 저장소 정리는 다음 경계 |

**왜 생성만 미루나.** 즉시 등장시키면 열거 중에 저장소가 바뀌어 그 자리에서 터집니다.
「열거하면서 만들지 마라」고 적어 두는 대신 **터질 수 없게** 만든 것입니다.

**왜 값 쓰기는 안 미루나.** 미루면 자기가 쓴 값을 자기가 못 읽습니다. 코드가 훨씬 어려워지고,
그렇게 해서 얻는 것이 없습니다.

만든 엔티티는 **존재는 즉시** 합니다 — 컴포넌트를 붙일 수 있고, `IsAlive`도 true이고,
`GetEntity(id)`로도 찾힙니다. 미뤄지는 것은 **쿼리에 잡히는 시점**뿐입니다.

---

## 예외 정책

```csharp
Systems.RethrowOnSystemException   // 에디터 기본 true, 빌드 기본 false
```

시스템이 던진 예외를 호출자까지 올릴지 정합니다.

삼키면 게임은 버팁니다. 그런데 **죽은 시스템이 갱신하지 못한 값을 뒤 시스템이 읽고
엉뚱한 곳에서 터지므로, 증상이 원인에서 멀어집니다.** 개발 중에는 첫 예외에서 멈추는 편이
원인을 찾기 쉽고, 빌드에서는 한 시스템의 실패로 게임 전체가 멈추지 않는 편이 낫습니다.

**빌드에서 삼킨다는 것은 곧 「빌드에서는 시스템이 죽어도 티가 안 난다」는 뜻**이기도 합니다.
중요한 빌드라면 로그를 확인할 경로를 따로 마련하세요.

---

## 안 하는 것

의도적으로 제공하지 않습니다. 필요해지면 **그건 이 패키지가 아니라 다른 도구를 쓸 때**입니다.

- **아키타입/청크 스토리지, 컴포넌트 struct 강제** — 엔티티 수백 규모에서
  `Dictionary<Type, IComponent>`로 충분합니다.
- **멀티스레드/Job** — 이 패키지는 메인 스레드 전용입니다.
- **네트워크 동기화 자체** — 스냅샷·델타 압축·예측·롤백은 하지 않습니다.
  `Context.Enqueue`가 「밖에서 온 것을 안전하게 받는 자리」를 줄 뿐, 보내고 받는 것은
  Netcode for GameObjects 같은 것이 합니다.

---

## 테스트

`Tests/Editor/`에 있습니다. **동작 명세는 이 테스트들이 소유합니다** —
문서와 코드가 갈리면 여기가 먼저 깨집니다.

Unity Test Runner(EditMode) 또는:

```
Unity.exe -batchMode -runTests -projectPath <프로젝트> -testPlatform EditMode -testResults results.xml
```
