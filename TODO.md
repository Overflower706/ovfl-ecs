# TODO

## 현재 버전
**v3.1.3**

## 계획
- **인박스 이벤트가 배출 레인에 묶인다.** `Systems.Tick`의 경계는 `PublishEvents`,
  `Systems.FixedTick`의 경계는 `PublishFixedEvents`인데 인박스는 하나다.
  인박스 안에서 `RaiseEvent`(비-fixed)를 부르고 배출이 `FixedTick`에서 일어나면
  그 이벤트는 다음 `Tick` 경계까지 잠든다. RPC 도착 시점은 고를 수 없으므로 부르는 쪽이 피할 방법이 없다.
  배출 직후 두 큐를 다 발행할지, 인박스를 레인별로 가를지 정해야 한다.
- **`Phase`가 여섯인 것이 맞는지 아직 증거가 없다.** `Simulation`·`Reaction`·`View`의 경계는
  써 보기 전에는 확신하기 어렵다. 「Phase로 나눈다」가 「등록 순서대로 돈다」보다 나은지도 같이 본다.
- **`Systems.FixedTick`이 `Boundary()`를 재사용하지 않는다.** 같은 일을 두 벌 적어 두어서
  한쪽만 고치는 사고가 난다. `Boundary(bool drainInbox, bool isFixed)` 하나로 합친다.
- **`GetEntitiesWith`가 매 호출 `new List<Entity>()`를 만든다.** `EntityListPool`이 이미 있는데 여기선 안 쓴다.
- **`IFixedCleanupSystem`은 쓰는 곳이 없다.** 다음 메이저에서 뺄 후보.

## 테스트 보완
- 인박스가 `FixedTick`에서 배출되는 경로에 테스트가 없다. 위 항목을 고칠 때 같이 세운다.
