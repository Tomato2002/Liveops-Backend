# myKONAMI — Đào sâu kiến trúc client

> Nguồn: REA `inspect-il2cpp-metadata` (tên type / method / field / event). Không có type của field/param (cần `libil2cpp.so` 64-bit) và không có thân method → **quan hệ giữa các lớp suy ra từ tên field + method**, không phải call graph thật.
> Tổng quan stack: [mykonami-tech-stack-analysis.md](mykonami-tech-stack-analysis.md).

## 1. Bức tranh tổng thể — 4 tầng

```
┌──────────────────────────────────────────────────────────────────────┐
│ APP SHELL (Assembly-CSharp)                                          │
│  Bootstrap ─► NetworkManager ─► Startup/Auth ─► Ava config ─► Lobby  │
│  ViewManager · DialogManager · ScreenManager · GlobalEventManager    │
├──────────────────────────────────────────────────────────────────────┤
│ META / LIVEOPS FEATURES (mỗi feature 1 namespace, cấu trúc lặp lại)  │
│  Shop · PrizePass · LiveEvents · Tournaments · Collectibles · SideBet│
│  DailyChallenge · PiggyBank · WinMachine · Rewards · Social ...      │
├──────────────────────────────────────────────────────────────────────┤
│ SLOT ENGINE  GameEngine2 (C#, Konami.Core)  ≈  GEL (playstudios.gel) │
│  GameController ► Stage/Strategy/Sequence/Wager/Network controllers  │
│  ~2.100 Strategy + ~800 ModelStrategy, cấu hình bằng JSON            │
├──────────────────────────────────────────────────────────────────────┤
│ LUA BRIDGE (PlayStudios.AM)  MoonSharp + Proxy C#→Lua                │
├──────────────────────────────────────────────────────────────────────┤
│ PLATFORM: PlayLink SDK (Kotlin) · IAP · Ads · Analytics · AssetBundle│
└──────────────────────────────────────────────────────────────────────┘
```

## 2. App shell

### 2.1 Bootstrap — điểm vào duy nhất
`Bootstrap` (singleton `Instance`) điều phối khởi động:
- `Awake/Start` → `SetInvariantCulture` → `OnPlayLinkInit` → `OnActivateUserConsentDomainManager` (consent/GDPR) → `OnGetServerTime` (lưu `SERVER_TIME_DELTA`) → `OnStartupComplete`.
- Reconnect: `Reconnect`, `ReconnectAsync`, `ReconnectAndWait`, `RECONNECT_TRIES_COUNT`, `Reauthenticate`, `OnApplicationFocus` (quay lại app → refresh).
- Facebook: `ConnectFacebook`, `FacebookConnectWithStartUpInfo`.
- Phụ thuộc (field): `assureLegalComplianceController`, `BlobStorageController`, `startupRefreshController`, timeout `MANIFEST_LOAD_TIMEOUT_SECS`.

Luồng khởi động suy ra:
```
Bootstrap.Awake
  → PlayLink init (identity, consent)
  → NetworkManager.Connect → Auth → AuthenticateWithStartup → LoadDynamicEndpoints (api/index)
  → server time delta
  → AssetManifestVersionsLoader (CDN) → AssetBundleManager
  → AvaConfigLoader (remote config)
  → ViewManager.LoadLobby
```

### 2.2 DI — Ninject dùng có chừng mực
- Chỉ thấy 1 Ninject module: `PlayStudios.DI.Utils.Modules.UtilityModule` (`Load`, `BindUtils`, bind `_dialogs`, `_analytics`, `_prefs`).
- Interface trong `PlayStudios.DI.Utils`: `IDialogManager`, `IViewManager`, `IEventManager`, `IPlayerPrefsAdapter`, `IPlayerProgressManager`, `ISocialManager`.
- **Facade gom dependency:** `ViewUtilityFacade` (`_viewManager`, `_analyticsManager`, `_dialogManager`, `_socialManager`), `PlayerUtilityFacade` (`_prefs`, `_progress`).
- **Adapter bọc singleton cũ:** `SocialAdapter` (bọc `_facebookManager`), `PlayerPrefsAdapter`.
- Phần lớn code cũ vẫn dùng **singleton** (`GetInstance()`: `EventManager`, `GlobalEventManager`, `NetworkController`, `ViewManager`, `AssetBundleManager`…).

→ Kiến trúc **lai**: codebase lâu năm dựa trên singleton; DI được đưa vào sau, qua interface + adapter + facade để dần tách phụ thuộc (pattern "strangler" điển hình).

### 2.3 Event / messaging — 3 cơ chế
| Cơ chế | Class | Vai trò |
|---|---|---|
| Global C# events (singleton) | `EventManager` | Event toàn app: `AccountAuthenticated`, `BalancesUpdate`, `AssetManifestUpdated`, `BuyCreditsSuccess`, `ShowSubView`… |
| Popup priority queue | `GlobalEventManager` | Xếp hàng popup theo độ ưu tiên (`GLOBAL_PRIORITY_*`: level up, quest complete, challenge…), `SupressPopUp`, `WaitForQueueToEmpty` → tránh popup chồng nhau |
| Command pattern | `CommandDispatcher` | `Dispatch`, `_supportedCommands`, `_activeCommands` (command: `PlayLinkLoginCommand`, `ServerPurchaseValidationCommand`, `HourlyBonusDooberCommand`…) |
| Engine events (string-based) | `EventsGroup` (GEL) | `RegisterEvent/UnRegisterEvent`, handler đặt tên `Handle_<EVENT>` (`Handle_MODEL_READY`, `Handle_CHANGE_STAGE`…) |
| Animation events | `*AnimationEvents`, `*AnimatorEvents` | Animator gọi về code (sound, rollup, spawn reward) |

Server → UI: `TriggerCommonDtoEvents` (`BalancesUpdate`, `LevelUpdate`, `LoyaltyUpdate`, `PlayerProgressUpdate`) — mỗi response có phần DTO chung (balance, level…) được tách ra và bắn event cho toàn app.

### 2.4 View / Dialog / Screen
- `IViewManager`: `LoadLobby`, `LoadGame`, `TransitionToMenu`, event `OnMenuLoaded/OnGameLoaded` → **2 "scene" logic: Lobby và Game (slot)**.
- `LobbyViewController`: các tab/menu là prefab (`MainMenuPrefab`, `RewardsMenuPrefab`, `TournamentsMenuPrefab`, `FavoritesMenuPrefab`, `BonusMenuPrefab`, `HighRollerMenuPrefab`…), `SwitchViews`, `PreloadLobbyContent`.
- `IDialogManager`: `ShowDialog`, `AddDialog` (queue), `ShowDialogInAssetBundle` → dialog nằm trong bundle, tải khi cần.
- `ScreenManager`: orientation **portrait + landscape** (`RequestOrientation`, `ForceOrientation`), phân loại aspect ratio (16x9, 4x3, non-standard), safe area, letterbox.
- View lifecycle kiểu iOS (GEL `ViewController`): `PrepareView → OnViewWillAppear → OnViewDidAppear → OnViewWillDisappear → OnViewDidDisappear → DestroyView`.

### 2.5 Remote config — "Ava"
`PlayStudios.Ava.Loading.AvaConfigLoader`:
- Nguồn: `LoadConfigFromServer` / `LoadConfigFromLocal`, `BatchLoadConfigs` (tải nhiều config 1 request).
- **4 lớp cache:** `_local`, `_deviceCache`, `_activeCache`, `_rawCache`.
- Gửi BI event khi config đổi (`SendConfigurationChangedBIEvent`).
- Mỗi feature có loader riêng: `SideBetConfigLoader`, `PPRAvaConfiigLoader`, `InboxAvaConfigLoader`… (`UpdateData`, `GetAvaConfig`).
- Ngoài ra `PlayStudios.FeatureToggles`, `PlayStudios.Segmentation` → feature flag + phân nhóm người chơi.

## 3. Meta / LiveOps features — module lặp lại cùng 1 khuôn

Mỗi feature (PrizePass, SideBet, LiveEvents, Tournaments…) có bộ thành phần giống nhau:

| Thành phần | Ví dụ (PrizePassRevamp / SideBet) |
|---|---|
| Config (Ava) | `PPRAvaConfiigLoader`, `SideBetConfigLoader` |
| Network models + Utf8Json formatter | `PrizePassRevamp.Networking.Models` + `Formatters.*` |
| Net controller | `PrizepassNetworkController` (`GetStatus`, `PostPrizeClaim`, `Cheat_*`) |
| Bundle loader (asset tải riêng) | `PPRBundleLoader`, `SideBetBundleLoader` (`StartBundleCachingIfNeeded`, `OnBundlesCached`) |
| Dialog loader | `PrizePassDialogLoader`, `LiveEvent_DialogLoader` (`networkTimeout`, `EventBlockerParent`) |
| View + animation events | `PPRContentAnimatorEvents`, `PPRRoundelAnimationEvents` |

→ **Feature = plugin tải động**: code nằm trong app, còn asset/config tải khi bật feature → bật/tắt, A/B test feature mà không cần ra bản mới. Có cả "Revamp" chạy song song bản cũ (`PrizePass` vs `PrizePassRevamp`).

## 4. Slot engine — GameEngine2 ≈ GEL

**GEL là bản fork mở rộng của GameEngine2**: cùng tên lớp, cùng method (`StageController`, `StrategyController`, `SequenceController`, `WagerController`, `GameModel`, `NetworkController`, `AModelStrategy`). GEL thêm: Lua, proxies, `RainCheckTags`, `ConvertJTokenToTypes`.

### 4.1 Thành phần
```
GameController  (IGameController)
 ├─ StageController     state machine các "stage" của máy (base game, free spin, bonus...)
 │                      ChangeStage, parseInheritedStages, RunCommands, transition enter/leave
 ├─ StrategyController  đọc JSON → tạo strategy bằng reflection
 │                      ProcessStrategies, ConvertJTokenToTypes, ProcessStrategyLinkage, ValidateTags
 ├─ SequenceController  chuỗi bước trình diễn (ProcessSequences, ParseSequence)
 ├─ WagerController     mức cược: GetLineBets, ValidateWager, costPerPayline, sideWager
 ├─ NetworkController   gom NetworkStrategy (addNetworkStrategy, processRequest, retry currentAttempts)
 ├─ GameModel           dữ liệu máy: ConfigJSONLoaded → MetaDataReady → processInternalModels
 │                      └─ AModelStrategy: map response server → model (getRemappedFields, translateData)
 └─ EventsGroup         event nội bộ máy (Handle_*)
GameViewController      tải art/prefab, HUD, widget meta (DailyChallenge, PiggyBank, SideBet, VideoAds)
```

### 4.2 Data-driven bằng JSON + Strategy
- Mỗi máy = **file config JSON** liệt kê strategy + tham số + liên kết giữa chúng; `StrategyController` dùng `reflectionHandler` để khởi tạo component strategy trên prefab.
- Thống kê tên strategy (GEL + GE2): reel 224, symbol 215, win 154, feature 110, anim 72, free spin 71, spin 63, pick 45, progressive 43, wild 40, jackpot 40, wheel 39, payline 31… → **thư viện "khối Lego"**: máy mới = ghép strategy có sẵn + art mới; chỉ khi cần hành vi mới mới viết strategy (C#) hoặc script (Lua).
- Có cả strategy **riêng từng máy** (`DiamondTrailsFeatureStrategy`, `MoneyTrailsFeatureStrategy`, `GeniesPalace*`, `UnwoolyRiches*`, `KGL*` …) → thư viện chung + phần đặc thù.

### 4.3 Luồng 1 lần spin (suy ra)
```
Spin button → WagerController.ValidateWager
  → SpinNetworkStrategy.SendSpinRequest (qua NetworkController → NetworkManager)
  → server trả kết quả (reel stop, wins, features)
  → AModelStrategy.processData → GameModel (Handle_MODEL_READY)
  → StageController/SequenceController chạy sequence: reel stop → win line → feature → rollup
  → TriggerCommonDtoEvents.BalancesUpdate → HUD cập nhật số dư
```

## 5. Lua bridge — `PlayStudios.AM`

| Lớp | Vai trò |
|---|---|
| `LuaGameScriptsManager` | Tải (`DownloadScript`), cache (`_loadedScripts`), tạo/xóa script |
| `LuaScript` | 1 script = state machine: `DoState`, `_currentState/_nextState`, `Call`, `CreateLuaStrategyFromJsonConfig`, `VerifyInclude` |
| `LuaRegisterTypesAndGlobals` | Đăng ký cho Lua thấy: `RegisterUnityClasses`, `RegisterSystemClasses`, `RegisterGE2Classes`, `RegisterNewtonsofJsonUtils`, `RegisterUnityProxies`… |
| `ProxiesManager` | Pool + cache proxy (`AddProxyToPool`, `GetProxyFromPool`) |
| `LuaGameObjectProxy`, `LuaJObjectProxy`, `LuaJArrayProxy` | **Lua không chạm trực tiếp object Unity**, chỉ qua proxy có API giới hạn (`SetActive`, `Find`, `GetComponent`, `TweenLocalScaleTo`…) |
| `ILuaCoroutineActionRunner` | Lua chạy coroutine qua C# (`ExecuteCoroutine`, `StopCoroutinesForScript`) |
| `LuaState` (GEL) | 1 stage của máy được viết bằng Lua (`OnEnter`, `OnExit`, `HasEventTransition`) |
| `JSONToLuaStrategy`, `LuaClassFromConfigStrategy` | Strategy trong JSON có thể trỏ sang class Lua |

→ Lua là **lớp mở rộng có kiểm soát**: script chỉ thấy proxy + API đã đăng ký → an toàn hơn, proxy được pool để giảm GC, và có thể ràng buộc phiên bản API (`GelWidgetMigrator`).

## 6. Bài học thiết kế rút ra

1. **Bootstrap tập trung** + startup tuần tự rõ ràng (identity → network → time → assets → config → lobby).
2. **Popup priority queue** là bắt buộc với game nhiều meta feature (tránh popup chồng nhau).
3. **Remote config nhiều tầng cache** (local / device / active / raw) → app khởi động được cả khi mạng chậm.
4. **Feature module theo khuôn cố định** (config + network + bundle + dialog + view) → nhiều team làm song song, bật/tắt bằng config.
5. **Slot engine data-driven**: JSON + Strategy pattern + reflection; code chỉ viết khi cần hành vi mới.
6. **Scripting qua proxy** thay vì expose thẳng Unity API.
7. **Nợ kỹ thuật thấy rõ:** 2 engine song song, singleton + DI lẫn lộn, bản "Revamp" song song bản cũ, endpoint `Cheat_*` trong client build release, Unibill còn sót → dấu hiệu codebase lâu năm (từ ~2013, theo cert ký app) phát triển liên tục.

## Câu hỏi chưa giải quyết
- Call graph thật (ai gọi ai), type của field → cần bản `arm64-v8a` + REA `binary_path` / Ghidra.
- Format file config JSON của máy slot (nằm trong asset bundle tải từ CDN, không có trong APK).
- Strategy được tạo bằng `AddComponent` (MonoBehaviour) hay object thuần.
