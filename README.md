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
"com.ovfl.ecs": "https://github.com/Overflower706/ovfl-ecs.git#3.1.2"
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

phase 개념이 '순서대로 동작한다'보다 나은지는 계속 고민이 되네...

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

## 네트워크 — `NetworkBehaviour`와 함께 쓸 때

**이 절이 이 패키지에서 제일 중요합니다.** 여기를 틀리면 재현 안 되는 버그가 생깁니다.

### 문제: RPC는 밖에서 아무 때나 불린다

```csharp
[Rpc(SendTo.ClientsAndHost)]
void ScoreChangedRpc(int value)
{
    context.GetUniqueComponent<ScoreComponent>().Value = value;   // ❌
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
    context.Enqueue(ctx => ctx.GetUniqueComponent<ScoreComponent>().Value = value);   // ✅
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

**언제 도착할지는 네트워크가 정하는 것이라 어떤 설계로도 고정할 수 없습니다.**
인박스가 바꾸는 것은 지연이 아니라 **적용 지점**입니다 — 「아무 데서나」가 「경계에서만」이 됩니다.
그래서 같은 입력에 대해 **시스템들이 보는 세계가 스텝마다 일관되고**, 로그의 `Tick`으로
무엇이 언제 들어왔는지 되짚을 수 있습니다.

> 배출 도중에 새로 들어온 것은 **그 다음 배출**로 넘어갑니다.
> 매 프레임 도착하는 RPC가 스텝을 영영 끝내지 못하게 만들지 않기 위해서입니다.

### 다리(bridge) — `NetworkObject`마다 하나

시스템을 `NetworkBehaviour`로 만들지 마세요. **`NetworkBehaviour`는 통신만 하고,
게임 로직은 순수 C# 시스템에 둡니다.** 하나로 두라는 뜻은 아닙니다 —
`NetworkVariable`은 `NetworkBehaviour`에, 그것은 다시 `NetworkObject`에 붙으므로
**상태가 붙어 있는 오브젝트를 따라갑니다.**

| 다리 | 몇 개 | 무엇을 담나 |
|---|---|---|
| **세션 다리** | 1 | 게임 전체 상태 — 점수, 남은 시간, 진행 단계, 로스터 |
| **오브젝트 다리** | 스폰된 수만큼 | 그 오브젝트의 상태 — 위치, 스킨, 체력 |

**전부 세션 다리에 몰지 마세요.** 고양이 열 마리의 위치를 세션 다리의
`NetworkList`로 들면 한 마리가 움직일 때마다 열 마리치가 오갑니다.
스폰되는 것의 상태는 그 오브젝트의 다리가 듭니다.

**공유되는 것은 인스턴스가 아니라 방식입니다.** 어느 다리든 하는 일은 셋뿐입니다 —
`Bind(context)`로 Context를 받고, 받은 것을 `Enqueue`로 넣고, RPC를 드러냅니다.
둘 다 아래 「시스템과 다리는 어떻게 주고받나」에 코드로 있습니다.

**상태는 `NetworkVariable`로, 사건은 RPC로.** 이 구분이 늦게 들어온 클라이언트를 살립니다.
RPC는 보낼 때 접속해 있던 사람에게만 갑니다 — 그래서 상태를 RPC로 보내면
**중간에 들어온 사람은 아무것도 모릅니다.** `NetworkVariable`은 접속하는 순간 현재 값을 받습니다.

| | 쓸 것 | 예 |
|---|---|---|
| **상태** — 지금 어떠한가 | `NetworkVariable` | 점수, 남은 시간, 준비 여부, 로스터 |
| **사건** — 방금 무슨 일이 있었나 | RPC | 점프했다, 맞았다, 버튼을 눌렀다 |

### 시스템과 다리는 어떻게 주고받나

**게임플레이 시스템은 다리를 몰라야 합니다.** 알게 되면 그 시스템은 네트워크 없이 테스트할 수
없고, 싱글플레이에서도 못 씁니다. 그래서 양방향 모두 **Context를 사이에 둡니다.**

```
들어옴   다리 → Context.Enqueue → (경계) → 컴포넌트 값 / 이벤트 → 시스템이 읽음
나감     시스템 → 이벤트 발행 → Phase.Outbox 시스템 하나가 → 다리를 부름
```

#### 다리를 Context에 등록합니다

시스템이 다리를 찾을 수 있도록 컴포넌트로 넣어 둡니다. **다리가 둘로 갈리는 만큼
컴포넌트도 둘입니다.**

```csharp
// 세션 다리 — Context에 하나뿐이라 이름으로 찾습니다
public class SessionBridgeComponent : IComponent
{
    public SessionBridge Bridge;
}

// 오브젝트 다리 — 그 오브젝트의 엔티티에 붙습니다
public class CatBridgeComponent : IComponent
{
    public CatBridge Bridge;
}
```

**오브젝트 다리를 「유일한 것」으로 찾으려 하면 안 됩니다.** 고양이가 열 마리면
`TryGetUniqueComponent<CatBridgeComponent>`는 언제나 실패합니다.
그것은 **엔티티에 매달린 값**이지 전역 값이 아닙니다.

```csharp
// Runner
private void Awake()
{
    context = new Context();
    systems = new Systems(context);

    context.CreateEntity().AddComponent(new SessionBridgeComponent { Bridge = sessionBridge });
    sessionBridge.Bind(context);          // 접속 시작 전에
    // 고양이 다리는 스폰될 때 자기 엔티티를 만들며 스스로 붙습니다 (아래)

    systems.Add(Phase.Simulation, new CoinPickupSystem());
    systems.Add(Phase.Reaction,   new ScoreHudSystem());
    systems.Add(Phase.Outbox,     new NetworkOutboxSystem());
}
```

#### 나가는 쪽 — 시스템은 «의도»만 남깁니다

**대상이 여럿일 수 있으므로 이벤트가 «누구에 대한 것인지»를 들고 갑니다.**
고양이 열 마리의 점수가 제각각 오르는 상황이 바로 이것입니다.

```csharp
public class ScoreRequestedEvent : EventComponent
{
    public Entity Target;   // 어느 고양이인가
    public int Amount;
}
```

```csharp
// Phase.Simulation — 네트워크를 전혀 모릅니다
public class CoinPickupSystem : ITickSystem
{
    public Context Context { get; set; }

    public void Tick()
    {
        foreach (var coin in Context.GetEntitiesWith<CoinComponent>())
        {
            var touchedBy = coin.GetComponent<CoinComponent>().TouchedBy;
            if (touchedBy == null) continue;

            Context.DestroyEntity(coin);
            Context.RaiseEvent(new ScoreRequestedEvent { Target = touchedBy, Amount = 10 });
        }
    }
}
```

```csharp
// Phase.Outbox — 다리를 아는 유일한 시스템
public class NetworkOutboxSystem : ITickSystem
{
    public Context Context { get; set; }

    public void Tick()
    {
        Context.ProcessEvents<ScoreRequestedEvent>((_, e) =>
        {
            // 이벤트가 발행된 뒤 그 고양이가 죽었을 수 있습니다.
            if (!Context.IsAlive(e.Target)) return;

            // 다리는 그 엔티티에 붙어 있습니다. 전역에서 찾지 않습니다.
            if (e.Target.TryGetComponent<CatBridgeComponent>(out var cat))
                cat.Bridge.RequestAddScoreRpc(e.Amount);
        });
    }
}
```

**RPC를 어느 고양이가 받을지는 NGO가 정합니다.** `cat.Bridge`는 그 고양이의
`NetworkObject`에 붙은 `NetworkBehaviour`이므로, 거기서 부른 RPC는
**서버의 그 오브젝트에 도착합니다.** 열 마리가 각자 자기 다리를 통해 보내면
서버에서도 열 개의 다른 오브젝트가 받습니다 — 식별자를 실어 보낼 필요가 없습니다.

**서버는 보낸 사람이 그 고양이의 주인인지 확인해야 합니다.** 클라이언트가 부르는
RPC는 무엇이든 조작될 수 있습니다.

```csharp
[Rpc(SendTo.Server)]
public void RequestAddScoreRpc(int amount, RpcParams rpc = default)
{
    if (rpc.Receive.SenderClientId != OwnerClientId) return;   // 남의 고양이
    if (amount is <= 0 or > 100) return;                       // 말이 되는 값인가
    score.Value += amount;
}
```

**이것이 `Phase.Outbox`가 있는 이유입니다.** 내보내는 일이 한 자리에 모이면
「이 스텝에서 무엇이 밖으로 나갔나」를 한 곳에서 볼 수 있고,
게임플레이 시스템은 계속 순수 C#으로 남습니다.

#### 들어오는 쪽 — 다리는 «적용»만 합니다

```csharp
// 세션 다리 — 게임 전체에 하나
public class SessionBridge : NetworkBehaviour
{
    private Context context;
    private readonly NetworkVariable<float> remainingTime =
        new(0f, writePerm: NetworkVariableWritePermission.Server);

    public void Bind(Context context) => this.context = context;

    public override void OnNetworkSpawn()
    {
        remainingTime.OnValueChanged += OnTimeChanged;
        PushTime(remainingTime.Value);   // ← 늦게 들어온 클라이언트가 현재 상태를 받는 곳
    }

    public override void OnNetworkDespawn() => remainingTime.OnValueChanged -= OnTimeChanged;

    private void OnTimeChanged(float _, float value) => PushTime(value);

    // 상태 → 컴포넌트 값
    private void PushTime(float value)
        => context?.Enqueue(ctx =>
        {
            if (ctx.TryGetUniqueComponent<TimerComponent>(out var t)) t.Remaining = value;
        });

    // 사건 → 이벤트
    [Rpc(SendTo.ClientsAndHost)]
    public void GameEndedRpc()
        => context?.Enqueue(ctx => ctx.RaiseEvent(new GameEndedEvent()));
}
```

**인박스에서 발행한 이벤트는 그 스텝에서 바로 읽힙니다.** 경계가
「인박스 배출 → 이벤트 발행 → 반영」 순서라, `Phase.Inbox`의 시스템부터 그것을 봅니다.

#### 스폰되는 것의 다리

스폰된 오브젝트는 **자기 엔티티를 스스로 들고 있으면 됩니다.** 그러면
`NetworkObjectId`로 엔티티를 되찾는 표를 따로 둘 필요가 없습니다.

```csharp
public class CatBridge : NetworkBehaviour
{
    private readonly NetworkVariable<int> score = new(writePerm: NetworkVariableWritePermission.Server);
    private Context context;
    private Entity entity;

    public void Bind(Context context) => this.context = context;

    public override void OnNetworkSpawn()
    {
        score.OnValueChanged += OnScoreChanged;

        context?.Enqueue(ctx =>
        {
            entity = ctx.CreateEntity();
            entity.AddComponent(new CatComponent { View = this });
            entity.AddComponent(new CatBridgeComponent { Bridge = this });   // ← 여기서 붙습니다
            entity.AddComponent(new ScoreComponent { Value = score.Value });
        });
    }

    public override void OnNetworkDespawn()
    {
        score.OnValueChanged -= OnScoreChanged;
        context?.Enqueue(ctx => ctx.DestroyEntity(entity));
    }

    private void OnScoreChanged(int _, int value)
        => context?.Enqueue(ctx =>
        {
            if (ctx.IsAlive(entity)) entity.GetComponent<ScoreComponent>().Value = value;
        });

    [Rpc(SendTo.Server)]
    public void RequestAddScoreRpc(int amount, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != OwnerClientId) return;
        score.Value += amount;
    }
}
```

`Bind`는 씬의 Runner가 스폰을 감지해 넘겨줍니다
(`NetworkManager.OnClientConnectedCallback`이나 스폰 훅에서).

**`IsAlive` 검사가 필요한 이유**: `OnValueChanged`가 despawn과 겹쳐 도착하면
`Enqueue`한 일이 배출될 때는 이미 엔티티가 없을 수 있습니다.
인박스는 **넣은 시점이 아니라 배출 시점의 세계**에서 실행됩니다.

**값이 여럿이면 하나로 묶으세요.** 위치·회전·상태를 각각 `NetworkVariable`로 두면
메시지도 그만큼 나갑니다. 함께 바뀌는 것은 `INetworkSerializable` struct 하나로 묶습니다.

```csharp
// Phase.Reaction — 어디서 온 값인지 몰라도 됩니다
public class ScoreHudSystem : ITickSystem
{
    public Context Context { get; set; }

    public void Tick()
    {
        foreach (var cat in Context.GetEntitiesWith<ScoreComponent>())
            hud.SetScore(cat.ID, cat.GetComponent<ScoreComponent>().Value);
    }
}
```

#### 한 바퀴 — 고양이 하나의 점수가 오를 때

```
[클라 A] CoinPickupSystem       Simulation  코인을 먹고 ScoreRequestedEvent{Target=고양이3}
[클라 A] NetworkOutboxSystem    Outbox      고양이3의 다리로 RequestAddScoreRpc(10)
[서버]   고양이3의 CatBridge                 주인 확인 후 score.Value += 10
[모두]   NetworkVariable 동기화              고양이3의 OnValueChanged → Enqueue
[모두]   경계                    Inbox 직전   고양이3 엔티티의 ScoreComponent.Value = 110
[모두]   ScoreHudSystem          Reaction    화면 갱신
```

**나머지 아홉 마리는 아무 일도 겪지 않습니다.** 오간 메시지도 고양이3의 것뿐입니다 —
상태가 그 오브젝트에 붙어 있기 때문입니다.

**「모두」가 같은 순간을 뜻하지는 않습니다.** 각자 자기 RTT만큼 뒤에 받고,
그래서 클라이언트마다 적용된 `Context.Tick` 값이 다릅니다 —
`Tick`은 **그 기계 안에서만 의미가 있는 번호**이지 기계 간 공통 시간선이 아닙니다.
로그를 맞춰 볼 때 이걸 착각하면 정상 동작을 버그로 읽게 됩니다.

`NetworkVariable`을 쓰는 이유가 여기 있습니다. 같은 순간이 아니라
**같은 결론**에 도달하는 것을 목표로 하기 때문에, 도착이 늦어도 어긋난 채로 남지 않습니다.

### 정리

1. 시스템은 순수 C#. `NetworkBehaviour`는 다리에만.
2. 밖에서 들어온 것은 **전부** `Context.Enqueue`.
3. 상태는 `NetworkVariable`, 사건은 RPC.
4. **다리를 아는 시스템은 `Phase.Outbox`의 하나뿐.** 나머지는 이벤트만 발행합니다.
5. 다리는 `Bind(context)`를 **접속 시작 전에** 받아야 합니다. `OnNetworkSpawn`이 먼저 올 수 있습니다.
6. `Context.Tick`은 **기계마다 다릅니다.** 클라이언트 간 시간 비교에 쓰지 마세요.

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

---

## 쿼리

```csharp
Context.GetEntitiesWith<T>()                    // 목록 (스냅샷)
Context.TryGetUniqueEntity<T>(out var entity)   // 정확히 하나일 때 true. 조용합니다
Context.TryGetUniqueComponent<T>(out var comp)
Context.GetEntity(id)
Context.AllEntities                             // 지연 열거
```

`GetEntitiesWith`는 **부르는 시점의 목록을 떠서** 줍니다. 결과를 돌면서 엔티티를
만들거나 지워도 안전합니다.

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
