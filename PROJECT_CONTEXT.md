# MASTER PROJECT CONTEXT (SINGLE SOURCE OF TRUTH)
# Dự Án: Slot LiveOps Engine (Kiến trúc myKONAMI / PLAYSTUDIOS Stack)

> **CẢNH BÁO QUAN TRỌNG CHO MỌI AGENT VÀ LẬP TRÌNH VIÊN:**  
> File này là **Single Source of Truth (SSOT - Nguồn chân lý duy nhất)** của toàn bộ dự án.  
> Mọi thông số, cấu trúc lớp, chuỗi khởi động, hợp đồng DTO và quy chuẩn kỹ thuật trong tài liệu này được trích xuất trực tiếp từ bản dịch ngược APK thực tế (`com.playstudios.mykonami` build 48967, Unity 6000.0.73f1 IL2CPP v31) thông qua công cụ REA (`morluto/rea`).  
> **BẤT KỲ HÀNH ĐỘNG NÀO** (tạo file, viết code C#, code Lua, thiết kế database hay refactor) **BẮT BUỘC PHẢI ĐỐI CHIẾU VỚI CÁC QUY TẮC TRONG FILE NÀY.**

---

## 1. TỔNG QUAN HỆ THỐNG & TRIẾT LÝ VẬN HÀNH

Hệ thống được thiết kế theo chuẩn công nghiệp casino/live-ops AAA (PLAYSTUDIOS / myKONAMI):
1. **Server-Authoritative:** Backend nắm giữ toàn bộ trạng thái tài chính, thuật toán ngẫu nhiên (RNG), bảng trả thưởng (Paytable), số dư ví (Balance). Client không bao giờ tự sinh kết quả quay hay tự cộng tiền.
2. **Stateless API:** Backend scale ngang không giới hạn, không lưu session trong RAM server. Trạng thái phân tán được quản lý qua Redis và Database.
3. **Unity 4-Layer Client:** Phân tách rành mạch giữa Vỏ ứng dụng (App Shell), Tính năng kinh doanh (Meta), Khung máy Slot (Slot Engine Strategy), và Vùng mở rộng Hot-Update (Lua Bridge).
4. **Data-Driven & Hot-Update:** Cấu hình máy slot bằng JSON kế thừa (`InheritedJson`), logic mở rộng bằng Lua MoonSharp qua lớp Sandbox an toàn (`LuaGameObjectProxy`).
5. **Zero-Alloc GC & High Throughput:** Client dùng UniTask + ObjectPool; Backend dùng Redis Distributed Lock + Utf8Json/Fast serialization.

---

## 2. KIẾN TRÚC TOÀN HỆ THỐNG (END-TO-END BLUEPRINT)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        BACKEND (ASP.NET Core Web API)                  │
│                                                                        │
│   [Api Layer]                                                          │
│     ├── Dynamic Discovery: IndexController (GET api/index)             │
│     ├── Controllers: AuthController, SlotController, WalletController  │
│     ├── Middlewares: ErrorHandlingMiddleware, Idempotency (X-Request-Id)│
│     └── ActionFilters: CommonDtoResponseFilter (Tự động nạp Balances)  │
│   [Application Layer]                                                  │
│     ├── Commands/Queries: SpinCommand, StartupCommand, ClaimPrizeCmd   │
│     └── DTOs: SpinRequest, SpinResult, CommonDto, BalancesDto          │
│   [Domain Layer]                                                       │
│     ├── Core Services: RNG, PaytableEvaluator, WagerValidator          │
│     └── Entities: Player, Wallet (Credits, Exp, Loyalty, Premium)      │
│   [Infrastructure Layer]                                               │
│     ├── Persistence: PostgreSQL / MySQL (EF Core + Dapper)             │
│     ├── Cache & Distributed Lock: Redis Redlock (Chống Double Spending)│
│     └── Fast Serialization: Utf8Json zero-alloc formatters             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTP/REST + JSON (CommonDto Pattern)
                                    │ Headers: X-Client-Version, X-Device-ID, X-Request-Id
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        CLIENT (Unity Engine C#)                        │
│                                                                        │
│ ┌────────────────────────────────────────────────────────────────────┐ │
│ │ TẦNG 1: APP SHELL (Lõi ứng dụng)                                   │ │
│ │  ├── Bootstrap: Khởi động tuần tự (Init->Consent->TimeSync->Lobby)  │ │
│ │  ├── NetworkManager: QueueTransaction, Header Inject, CommonDto Evt│ │
│ │  └── GlobalEventManager: Popup Priority Queue (Ngăn chặn đè Popup)  │ │
│ ├────────────────────────────────────────────────────────────────────┤ │
│ │ TẦNG 2: META / LIVEOPS FEATURES (Mô-đun cắm rút độc lập)           │ │
│ │  ├── Khuôn mẫu 4 phần: ConfigLoader + NetController +              │ │
│ │  │                     BundleLoader + DialogLoader                 │ │
│ │  ├── Modules: Shop, PrizePass, SideBet, WinMachine, Jackpots        │ │
│ │  └── Injected Spin Results: Tích hợp trực tiếp vào vòng lặp Spin   │ │
│ ├────────────────────────────────────────────────────────────────────┤ │
│ │ TẦNG 3: SLOT ENGINE (GameEngine2/GEL - Strategy Pattern + JSON)    │ │
│ │  ├── Contract: IGameApi (Init, StartGame, RegisterEvent, Sound)    │ │
│ │  ├── Controllers: StageController, StrategyController,             │ │
│ │  │                SequenceController, WagerController, GameModel   │ │
│ │  ├── Reel & Symbol: IReelController, ReelStrips, ObjectPool<Symbol> │ │
│ │  └── Presentation: RollUpTextField, CyclePayline, WinCelebration   │ │
│ ├────────────────────────────────────────────────────────────────────┤ │
│ │ TẦNG 4: LUA BRIDGE (MoonSharp Pure C# Lua Sandbox)                 │ │
│ │  ├── Interop Manager: ProxiesManager + AMProxyUserDataDescriptor   │ │
│ │  ├── Safe Proxies: LuaGameObjectProxy, LuaTransformProxy, TMPProxy │ │
│ │  └── Hot-Update: LuaGameScriptsManager tải script từ CDN           │ │
│ └────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. ĐẶC TẢ CHI TIẾT TỪNG TẦNG (REVERSE-ENGINEERED SPECIFICATIONS)

### 3.1. Tầng 1: App Shell (Lõi Ứng Dụng)

#### Chuỗi Khởi Động Chuẩn (`Bootstrap.cs`)
Khởi động ứng dụng bắt buộc thực hiện tuần tự không nhảy cóc:
1. `Awake()`: Thiết lập môi trường, `SetInvariantCulture()` (tránh lỗi parse dấu chấm/phẩy thập phân trên các thiết bị đa ngôn ngữ).
2. `OnPlayLinkInit()`: Khởi tạo định danh thiết bị / SDK người dùng.
3. `OnActivateUserConsentDomainManager()`: Xác nhận sự đồng thuận chính sách người dùng (GDPR / Privacy).
4. `OnGetServerTime()`: Đồng bộ thời gian với server, tính toán và lưu biến `SERVER_TIME_DELTA`.
5. `LoadDynamicEndpoints()`: Gửi request `GET api/index` để lấy danh sách endpoint động từ Backend.
6. `AssetManifestVersionsLoader` & `AvaConfigLoader`: Nạp manifest AssetBundle từ CDN và Remote Config.
7. `OnStartupComplete()` -> Chuyển sang `ViewManager.LoadLobby()`.
8. Cơ chế Reconnect: Hỗ trợ `ReconnectAsync()`, `Reauthenticate()`, tự động kích hoạt khi ứng dụng `OnApplicationFocus(true)`.

#### Quản Lý Mạng Chuẩn (`PlayStudios.Network.NetworkManager`)
- **Hàng Đợi Giao Dịch (Transaction Queue):**
  - Mọi request gửi đi đều phải bọc qua `QueueTransaction()`, xếp vào `ProcessRequestQueue()`.
  - Hỗ trợ `CancelActiveRequest()`, kiểm tra `IsQueueEmpty()`, `IsNetworkIdle()`.
- **Dynamic Headers Bắt Buộc Gắn Vào Mỗi Request:**
  - `X-Client-Version`: Phiên bản client (e.g. `1.140.0`).
  - `X-Device-ID`: Mã định danh thiết bị duy nhất.
  - `X-Device-Type`: Loại thiết bị (`Android`, `iOS`, `Editor`).
  - `X-Platform-Type`: Nền tảng phân phối (`GooglePlay`, `AppleStore`).
  - `X-ENGINE`: Phiên bản engine (`Unity 6000.0.73f1`).
  - `X-Request-Id`: Idempotency GUID (ngăn chặn duplicate do lag mạng).
- **Phục Hồi & Tín Hiệu Server (Server Signals):**
  - `add_OnFatalFailure` / `add_OnNetworkError`
  - `add_OnUpgradeRequired`: Yêu cầu cập nhật phiên bản mới.
  - `add_OnMultipleDeviceActivity`: Cảnh báo đăng nhập trên thiết bị khác.
  - `add_OnAccountReset`: Reset tài khoản.

#### Hàng Đợi Popup Ưu Tiên (`GlobalEventManager`)
- Tuyệt đối không cho phép popup/dialog mở đè lên nhau.
- Quản lý qua hàng đợi ưu tiên `PopupPriority`:
  - `SupressPopUp()` / `PopUpEventsSupressed()`
  - `coWaitForQueueToEmptyAndFinish()`: Chờ người chơi đóng popup hiện tại rồi mới kích hoạt popup tiếp theo theo thứ tự ưu tiên (Level Up -> Quest Complete -> Daily Challenge -> Shop Offers).

---

### 3.2. Tầng 2: Meta / LiveOps Features (Mô-Đun Độc Lập)

Mỗi tính năng Meta (Shop, PrizePass, SideBet, WinMachine, Jackpots, DailyBonus) là một module độc lập tuân thủ bộ 4 thành phần:
1. **`*ConfigLoader` (Ava Config System):** 
   - Quản lý Remote Config đa tầng: `_local` (mặc định) -> `_deviceCache` (lưu đĩa) -> `_activeCache` (RAM) -> `_rawCache`.
   - Hỗ trợ `BatchLoadConfigs` tải nhiều cấu hình trong 1 HTTP request.
2. **`*NetworkController`:**
   - Điều khiển giao tiếp nghiệp vụ riêng (`GetStatus`, `PostPrizeClaim`, `Cheat_*`).
3. **`*BundleLoader`:**
   - Quản lý tải và giải phóng AssetBundle độc lập từ CDN (`StartBundleCachingIfNeeded`, `OnBundlesCached`).
4. **`*DialogLoader`:**
   - Nạp View/Dialog từ AssetBundle và đưa vào hàng đợi `GlobalEventManager`.

**Injected Meta Spin Results:**
Khi người chơi Spin, Backend trả về payload tích hợp các tính năng Meta đang hoạt động. Các Formatter tương ứng bóc tách dữ liệu:
- `PrizePassRevampSpinResult`
- `SideBetSpinResult`
- `WinMachineSpinResult`
- `DragonJackpotSpinResult`
- `LeaderboardSpinResult`

---

### 3.3. Tầng 3: Slot Engine (GameEngine2 / GEL Strategy Architecture)

Slot Engine được tách rời hoàn toàn khỏi MonoBehavior, vận hành theo **Strategy Pattern** và điều phối bằng dữ liệu JSON (`IGameConfiguration`).

#### Hợp Đồng App ↔ Máy Slot (`IGameApi`)
```csharp
public interface IGameApi
{
    void Initialize(IGameConfiguration config);
    void StartGame();
    void RegisterEvent(string eventName, Action<object> handler);
    void DispatchEvent(string eventName, object eventData);
    void DestroyGame();
    void PauseSoundEffects();
    void ResumeSoundEffects();
    void PauseMusic();
    void ResumeMusic();
}
```

#### Mô Hình Request & Spin Network (`SpinNetworkStrategy`)
- Client gửi lệnh Spin qua `SpinNetworkStrategy` (`SendSpinRequest`, `SendFreeSpinRequest`, `OnSpinResultSuccess`).
- Các model request chuẩn trích xuất từ IL2CPP:
  - `SpinRequest`: Gồm `SO` (Strategy Options), `CLP` (Client Paylines / Lines Played), `FSRT` (Free Spin Remaining Token).
  - `SpinRequest_FullWager`: Gồm `FullWager`, `FSRT`.
  - Hỗ trợ các kiểu cược: `fullWagerRequest`, `costDescription`, `freeSpinForFullWager`, `clientPaylinesWithSideWager`.

#### Bộ Các Controller Trọng Yếu
- **`StageController`:** Quản lý vòng đời trạng thái của máy (BaseGame, FreeSpins, PickGame, Transition).
  - Bắt các sự kiện chuỗi: `Handle_MODEL_READY`, `Handle_VIEW_READY`, `Handle_CHANGE_STAGE`, `Handle_TRANSITION_STATE_ENTER_COMPLETE`.
- **`StrategyController`:** Khởi tạo danh sách Strategy từ file JSON cấu hình máy (`ProcessStrategies`, `FinalizeStrategies`, `ConvertJTokenToTypes`).
- **`SequenceController`:** Điều phối chuỗi cuộn quay (Anticipation spin, Reel stop delay, Scatter tease).
- **`WagerController`:** Quản lý mức cược (`ValidateFullWagerIndex`, `Handle_CHANGE_WAGER_ATOMIC`).
- **`GameModel`:** Lưu trữ Model cấu hình máy (`ConfigJSONLoaded`, `processInternalModels`, `ClearModelData`).

#### Cuộn Quay & Trình Diễn Dừng Cuộn (Reel Stop Sequence)
- **`IReelController` & `ReelStrips`:**
  - Quản lý dải cuộn ảo hóa (Virtualized Reel Strips).
  - Quản lý SubSymbol và FakeSymbol để tạo hiệu ứng thị giác mượt mà (`ClaimSubSymbol`, `ReleaseSubSymbol`, `ShouldShowFakeSymbol`, `HasFakeSymbolsSetForReelIndex`).
  - Toàn bộ SymbolView tái sử dụng qua `UnityEngine.Pool.ObjectPool<SymbolView>`.
- **Trình tự dừng cuộn:**
  - Thực thi tuần tự qua `StopSpinAtPosition` -> `OnReelStop` -> `PlayReelStopSound` (`ReelStopSoundStrategy`) -> `StopSymbolFX`.
- **Payline Presentation & Cycling:**
  - `PaylineModelStrategy`: Bóc tách thông tin đường thắng (`PaylineInfo`: `PaylineIndex`, `Multiplier`, `BaseWin`).
  - Chuỗi hiển thị đường thắng: `CyclePaylineOutcome`, `HighlightPaylineSymbols`, `TraceSinglePayline` lần lượt hiển thị viền sáng từng đường thắng khi ở trạng thái rảnh (Idle Win).
- **Rollup & Win Celebration:**
  - `RollUpTextField`: Nhảy số coin thắng mượt mà không cấp phát chuỗi (`TMP_Text.SetText`), hỗ trợ `BangUpThreshold`, ngắt hiệu ứng khi người chơi chạm màn hình (`RollUpTextInstant`).
  - Phân cấp cấp độ thắng: `SmallWin` (`coAllPaylineWinSmall`) -> `MediumWin` (`coAllPaylineWinMedium`) -> `HugeWin` (`coAllPaylineWinHuge`) -> `EpicWin` (`coAllPaylineWinEpic`).
  - Hiệu ứng rơi coin: `CreateWinDooberEffect`, `CreateWinDooberEffectNoFly`.

---

### 3.4. Tầng 4: Lua Bridge & Sandbox (MoonSharp Isolation)

Để cập nhật máy slot mới hoặc chỉnh sửa tính năng mà không cần build lại APK/IPA (tuân thủ chính sách App Store và giải quyết vấn đề AOT của IL2CPP), dự án sử dụng Lua MoonSharp chạy trong Sandbox an toàn tuyệt đối.

#### Quy Tắc Sandbox Tuyệt Đối
- **Nghiêm cấm 100%:** Lua KHÔNG ĐƯỢC PHÉP dùng reflection gọi thẳng `CS.UnityEngine.GameObject` hay các API native của Unity.
- **Proxy Pattern:** Mọi tương tác bắt buộc phải thông qua các Proxy class kế thừa `ILuaProxy`:

```
PlayStudios.AM.Proxies/
├── ProxiesManager.cs                     # Quản lý vòng đời và cache các proxy instance
├── AMProxyUserDataDescriptor.cs          # Đăng ký kiểu an toàn với MoonSharp
├── LuaGameObjectProxy.cs                 # Bọc GameObject (SetActive, Find, PositionTo, AnimationPlay...)
├── LuaTransformProxy.cs                  # Bọc Transform (Position, LocalScale, Rotation...)
├── LuaTextMeshProUGUIProxy.cs            # Bọc Text UI (SetText, SetTextColor, RollUpText...)
├── LuaAnimatorProxy.cs                   # Bọc Animator (Play, SetTrigger, SetFloat, CrossFade...)
├── LuaSpriteRendererProxy.cs             # Bọc SpriteRenderer (SetSprite, SetColor...)
├── MetaProxies/
│   ├── LuaDialogProxy.cs                 # Điều khiển mở dialog từ Lua
│   └── LuaBaseButtonProxy.cs             # Bắt sự kiện Click an toàn
└── GE2NetworkProxy.cs                    # Cho phép Lua gửi request an toàn qua NetworkManager
```

#### Phương Thức Trọng Yếu Của `LuaGameObjectProxy`
- Điều khiển: `SetActive(bool)`, `SetChildrenActive(bool)`, `Find(string)`.
- Chuyển động/Tween: `PositionTo()`, `LocalPositionTo()`, `TweenLocalScaleTo()`, `LocalRotateToLoop()`.
- Hoạt họa: `AnimationPlay(string)`, `AnimationPlayAndWaitForEvent(string, string)`, `AnimationSetTrigger(string)`.
- Hiển thị Text & Coin: `SetText(string)`, `RollUpText(double, double, float)`, `RollUpTextInstant(double)`.
- Nút bấm: `SetButtonEnabled(bool)`, `RegisterButtonPressedCallback(closure)`.

---

### 3.5. Hợp Đồng DTO & Giao Thức Server-Authoritative

#### Đối Tượng Số Dư Chuẩn (`Balances`)
Mọi giao dịch làm thay đổi ví tiền đều đi kèm đối tượng `Balances` chuẩn:
```csharp
public class BalancesDto
{
    public long Credits { get; set; }       // Tiền xu quay slot
    public long Experience { get; set; }    // Điểm kinh nghiệm tích lũy
    public long Loyalty { get; set; }       // Điểm trung thành myVIP
    public long Premium { get; set; }       // Tiền tệ đặc biệt / Gems
}
```

#### Cơ Chế Tự Động Đồng Bộ (`CommonDto`)
Response của bất kỳ API nghiệp vụ nào (Spin, Claim, Purchase) đều được Server đính kèm `CommonDto`:
```json
{
  "result": { ... },
  "commonDto": {
    "balances": {
      "credits": 2500000,
      "experience": 12850,
      "loyalty": 450,
      "premium": 25
    },
    "level": 42,
    "levelIndexPoint": 850
  }
}
```
Client nhận response, `NetworkManager` tự động trích xuất `CommonDto` và kích hoạt `TriggerCommonDtoEvents`:
- `BalancesUpdate(Balances)` -> Cập nhật hiển thị số dư coin trên Top Bar.
- `LevelUpdate(int level, long exp)` -> Cập nhật thanh tiến trình Level.
- `LoyaltyUpdate(long loyalty)` -> Cập nhật điểm VIP.

#### Chống Hack & Concurrency Tại Backend
- **Redis Distributed Lock (Redlock):** Mọi request `api/spin` hoặc thay đổi ví phải acquire lock theo Key `lock:wallet:{playerId}` trước khi đọc hoặc ghi database.
- **Idempotency Key:** Kiểm tra `X-Request-Id` trong Redis với TTL 120s. Nếu request bị trùng (do retry mạng), trả về ngay kết quả đã cache, tuyệt đối không trừ tiền hay quay lần 2.

---

## 4. CẤU TRÚC THƯ MỤC CHUẨN CỦA WORKSPACE

Tất cả mã nguồn phát triển mới bắt buộc phải nằm đúng vị trí trong cây thư mục sau:

```text
e:\spin/
├── docs/                                  # Tài liệu phân tích dịch ngược APK gốc
├── shared/                                # Dùng chung giữa Client và Server
│   ├── contracts/                         # Schema JSON DTOs, API models, Endpoints
│   ├── configs/                           # JSON cấu hình máy slot (reels, paytables, stages)
│   └── lua_scripts/                       # Scripts Lua mẫu chạy trong MoonSharp Sandbox
│
├── server/                                # BACKEND (ASP.NET Core Web API)
│   ├── src/
│   │   ├── SpinEngine.Domain/             # Pure C# Logic: RNG, PaytableEvaluator, Wallet Rules
│   │   ├── SpinEngine.Application/        # UseCases, SpinCommandHandler, DTOs, Interfaces
│   │   ├── SpinEngine.Infrastructure/     # EF Core, Redis Redlock, DynamicEndpointsProvider
│   │   └── SpinEngine.Api/                # Controllers, Middlewares, ActionFilters, Program.cs
│   └── tests/                             # Unit tests kiểm tra RNG, Paytable, Concurrency
│
└── client/                                # UNITY CLIENT (Unity 6 / C#)
    ├── Assets/
    │   ├── _Project/
    │   │   ├── 1_AppShell/                # Bootstrap, NetworkManager, GlobalEventManager
    │   │   ├── 2_MetaFeatures/            # Shop, PrizePass, DailyBonus (Config, Net, Bundle, Dialog)
    │   │   ├── 3_SlotEngine/              # Stage, Strategy, ReelPool, RollUp, PaylinePresentation
    │   │   ├── 4_LuaBridge/               # MoonSharp runner, ProxiesManager, LuaGameObjectProxy
    │   │   └── Shared/                    # UniTask utils, Audio, Asset management
    │   └── ThirdParty/                    # MoonSharp, UniTask, TextMeshPro, Utf8Json
```

---

## 5. CHECKLIST BẮT BUỘC TRƯỚC MỌI HÀNH ĐỘNG CODE (GATEWAY RULES)

Mỗi khi AI hoặc developer viết hoặc chỉnh sửa bất kỳ file nào, **BẮT BUỘC** phải tự kiểm tra 4 cổng kiểm soát này:

### Cổng 1: Backend Safety Gate
- [ ] Logic có bị phụ thuộc vào RAM cục bộ không? (Phải đảm bảo Stateless).
- [ ] Mọi thay đổi ví tiền (Wallet) đã được bọc trong **Distributed Lock (Redlock)** chưa?
- [ ] API thanh toán hoặc Spin đã có kiểm tra **Idempotency Key (`X-Request-Id`)** chưa?
- [ ] Response nghiệp vụ đã đính kèm **`CommonDto`** (`Balances`, `Level`, `Exp`) chưa?

### Cổng 2: Unity Performance & Zero-Alloc Gate
- [ ] Có xuất hiện `System.Threading.Tasks.Task` hay `Coroutine` không? (Chỉ được dùng **`UniTask`**).
- [ ] Method async đã nhận `CancellationToken` (ví dụ `this.GetCancellationTokenOnDestroy()`) chưa?
- [ ] Có dùng LINQ hay phép cộng chuỗi `+` trong hàm lặp/Update không? (Phải dùng `TMP_Text.SetText` hoặc cache).
- [ ] Đối tượng sinh/hủy liên tục (symbol, particle, coin) đã qua `UnityEngine.Pool.ObjectPool<T>` chưa?
- [ ] Mọi Tween đã được gắn `.SetLink(gameObject)` để tránh rò rỉ bộ nhớ chưa?

### Cổng 3: Lua Sandbox Safety Gate
- [ ] Script Lua có cố gọi trực tiếp Unity API native (`CS.UnityEngine.GameObject`) không? (Bắt buộc dùng `LuaGameObjectProxy`).
- [ ] File Lua có theo cấu trúc module đóng gói (`local M = {}; return M`) không?
- [ ] Có bị rò rỉ biến toàn cục (global variable) trong Lua không?

### Cổng 4: Slot Strategy Decoupling Gate
- [ ] Logic của máy slot có bị hardcode vào MonoBehavior không? (Bắt buộc tách thành Strategy độc lập cấu hình qua JSON).
- [ ] Dữ liệu kết quả spin (Model) có bị trộn lẫn với hiệu ứng cuộn quay (View) không?

---

## 6. NHẬT KÝ TRẠNG THÁI & TIẾN ĐỘ DỰ ÁN (STATE TRACKER)

| Hạng mục | Trạng thái | Ghi chú |
| :--- | :--- | :--- |
| **Dịch ngược & Đặc tả kiến trúc (REA)** | ✅ Hoàn thành | Đã reverse APK, phân tích 127 assemblies, trích xuất đầy đủ 4 tầng |
| **Tài liệu & Tiêu chuẩn SSOT** | ✅ Hoàn thành | `PROJECT_CONTEXT.md`, `GEMINI.md`, `AGENTS.md`, `docs/` |
| **Shared Contracts & Configs** | ✅ Hoàn thành | Đã tạo `CommonDto`, `BalancesDto`, `SpinRequest`, `SpinResult`, `classic_slot_machine.json` |
| **Backend Core (Domain & Api)** | ✅ Hoàn thành | ASP.NET Core: Clean Architecture (Domain, App, Infra, Api), RNG, Paytable, Redlock, Idempotency |
| **Client App Shell (Bootstrap & Net)** | ⏳ Chưa bắt đầu | Bootstrap, NetworkManager với TransactionQueue, GlobalEventManager |
| **Client Slot Engine (GEL & Pool)** | ⏳ Chưa bắt đầu | Reel Controller, Symbol Pool, Wager Controller, Stage Controller |
| **Lua Bridge (MoonSharp Sandbox)** | ⏳ Chưa bắt đầu | `LuaGameObjectProxy`, ProxiesManager và runner MoonSharp |

---
*Mọi thay đổi lớn về kiến trúc hoặc tech stack đều phải cập nhật lại file này trước khi tiến hành code.*
