# myKONAMI 1.140.0 — Phân tích tech stack

> Nguồn: `myKONAMI®+Casino+Slot+Machines_1.140.0_APKPure.xapk` (package `com.playstudios.mykonami`, build 48967).
> Phương pháp: liệt kê file APK + decode `global-metadata.dat` bằng REA (`npx rea-agents inspect-il2cpp-metadata`, metadata v31: 127 assembly, 31.555 type, 223.601 method).
> Kiến trúc chi tiết: [mykonami-architecture-deep-dive.md](mykonami-architecture-deep-dive.md).
> **Giới hạn:** chỉ đọc tên type/method/field/string literal, KHÔNG decompile thân method → luồng xử lý là suy luận từ tên.

## 1. Engine & nền tảng

| Hạng mục | Giá trị | Bằng chứng |
|---|---|---|
| Engine | Unity **6000.0.73f1** | header `globalgamemanagers` |
| Scripting backend | **IL2CPP** | `libil2cpp.so` (~104 MB), `global-metadata.dat` v31 |
| ABI | `armeabi-v7a` (bản XAPK này) | `config.armeabi_v7a.apk` |
| Render pipeline | **Built-in** + Post Processing v2 | không có `Unity.RenderPipelines.*`; có `Unity.Postprocessing.Runtime` |
| Ngôn ngữ | C# + **Lua** | `Playstudios.Moonsharp` |

## 2. Assembly chính (theo số type)

| Assembly | Type | Vai trò |
|---|---|---|
| `Assembly-CSharp` | 13.236 | Code game chính (lobby, shop, live event, GameEngine2) |
| `playstudios.gel` | 2.272 | Game Engine Lua (GEL) |
| `playstudios.gel.konami` | 1.378 | Mở rộng GEL cho máy Konami |
| `PlayStudios.Konami.Core` | 858 | Network, Purchasing, Analytics, Spine runtime, Adjust |
| `AWSSDK.*` | ~975 | Kinesis Firehose, Cognito |
| `Xsolla` | 477 | Web shop |
| `Playstudios.Moonsharp` | 417 | Lua interpreter (fork riêng) |
| `Utf8Json` / `Newtonsoft.Json` | 386 / 494 | JSON |
| `UniTask` / `Ninject` | 329 / 224 | Async / DI |
| `playstudios.components` | 240 | Visual effects (MotionBlur, Tween, VertexControl…) |

## 3. Tech stack client

| Nhóm | Công nghệ |
|---|---|
| UI | uGUI, TextMeshPro |
| Animation 2D | **Spine** (207 type), Unity 2D Animation + IK, Timeline |
| Tween | **GoKit** (`playstudios.gokit`) + tween tự viết. **Không có DOTween** (0 type `DG.Tweening`; chỉ có assembly `UniTask.DOTween`) |
| Shader | AllIn1SpriteShader |
| Async | UniTask |
| DI | Ninject |
| Serialization | Utf8Json (~699 formatter sinh sẵn), Newtonsoft (phụ), FlatBuffers |
| Compression | SharpZipLib, Ionic.Zlib |
| Performance | Burst, Collections, Mathematics |

## 4. Slot engine — 2 thế hệ song song

- **`GameEngine2`** (C#, trong `Assembly-CSharp`): `GameEngine2.Strategies` 1.083 type, `ModelStrategies` 532 type.
- **`PlayStudios.GameEngineLua` (GEL)**: ~620 Strategy + ~270 ModelStrategy; logic máy slot bằng Lua tải từ CDN.
  - `LuaGameScriptsManager`: `DownloadScript`, `LoadScriptDataIntoScript`, `RegisterTypeToUserData`, `RegisterGlobalsToScript`.
- Contract app ↔ máy slot: `IGameApi` (`Initialize`, `StartGame`, `RegisterEvent`, `DispatchEvent`, `DestroyGame`, pause/resume sound).
- Pattern chung: **Strategy pattern** — mỗi hành vi (spin, feature, jackpot, pick game…) là 1 strategy, ghép lại thành 1 máy.
- Lý do dùng Lua: IL2CPP (AOT) không nạp được C# mới lúc runtime, Apple cấm tải native code → Lua cho phép ra máy mới / hotfix không cần update app; MoonSharp pure C# → chạy cả WebGL.

## 5. Networking

**Mô hình:** HTTP/REST + JSON, **server-authoritative**, không có realtime socket cho gameplay (websocket-sharp có trong build nhưng không type PlayStudios nào dùng tên Socket; URL `wss://` duy nhất là Xsolla).

```
Feature controllers (~35)            Slot engine strategies
ShopController, LobbyController...    SpinNetworkStrategy, PickNetworkStrategy...
            │                                   │
            └─────────► NetworkManager ◄────────┘   (GEL: RequestManager.PerformRequest)
                        QueueTransaction → SimpleQueue → ProcessRequestQueue
                        GetHeaders + ApplyDynamicHeaders
                        ▼
                   UnityWebRequest → https://konami-slots.playstudios.com/konami-slots/api/
```

**`PlayStudios.Network.NetworkManager`** (`Konami.Core`):
- Queue: `QueueTransaction`, `SimpleQueue`, `ProcessRequestQueue`, `IsQueueEmpty`, `CancelActiveRequest`.
- Khởi động: `Connect` → `Auth` → `AuthenticateWithStartup` → `LoadDynamicEndpoints` (endpoint lấy động từ `api/index`).
- Phục hồi: `Recover`, `RestoreNetwork`, event `OnRecoveryBegin/Complete`, `OnNetworkRestored`.
- Session: `RestartSession`, `ResetSessionExpTime`, `CheckForSessionBlockingErrors`.
- Server → client signal: `OnUpgradeRequired`, `OnMultipleDeviceActivity`, `OnAccountReset`, `OnFatalFailure`, `OnGetServerTime`.
- Retry an toàn: `HttpMethodExtensions.IsIdempotent`.

**Khác:**
- Auth: `IAuthenticationProvider` → Device / Token / Apple + Facebook (cả Limited Login), PlayLink (PlayStudios ID).
- Heartbeat: `HeartbeatManager` (`StartHeartbeat`, `SendHeartbeatAfterInterval`, event `AuthenticationExpired`).
- Header tự gắn: `X-Client-Version`, `X-Device-ID`, `X-Device-Ad-ID`, `X-Platform-Type`, `X-Store-Type`, `X-ENGINE`, `X-SDK`, `X-Client-Context`, `X-BUILD-PLATFORM`.
- Slot network strategies (`GameEngine2.NetworkStrategies`): `SpinNetworkStrategy`, `SideWagerSpinNetworkStrategy`, `PickNetworkStrategy`, `GE2Pick*GameNetworkStrategy`, `MLMScratchCardGameNetworkStrategy`, `TournamentNetworkStrategy`; base `ANetworkStrategy` có cache (`getCachedData`).
- Feature controllers (`MyVegas.Mobile.Network.Controllers`): Shop, Lobby, Balances, DailyBonus, HourlyBonus, PiggyBank, PrizePass, Collectibles, Event, Rewards, VideoAd, AppCharge, Xsolla, AssetManifest… Nhiều controller có method `Cheat_*` / `Debug*` → backend có endpoint cheat cho QA.

## 6. Asset delivery

- `AssetBundleManager` tự viết (`UpdateAssetManifestFromCdn`, `IsBundleCached`, `UnloadUnusedBundles`…) + manifest có version.
- CDN Akamai: `https://pscdndev-a.akamaihd.net/cdn/mobile/myVEGAS_Konami_Mobile/AssetManifestVersions.json`, `.../cdn/liveops/`.
- Không dùng Addressables (không có thư mục `aa/`).
- APK chứa 5.419 serialized file đặt tên theo GUID trong `assets/bin/Data/`.

## 7. Backend (suy luận)

| Hạng mục | Kết luận | Độ tin cậy |
|---|---|---|
| Ngôn ngữ / framework | **C# / ASP.NET Web API** | Cao — string còn sót `/../../../dev/backend/MyVegas.Mobile.Slots.WebApi/Content/` và `/LocalCDN` (tên project kiểu .NET, thư mục `Content/` của ASP.NET) |
| Codebase | Monorepo client + backend; backend dùng chung với myVEGAS Slots | Trung bình |
| Endpoint | `https://konami-slots.playstudios.com/konami-slots/api/` | Cao |
| Serialization | JSON, có `$type` (Newtonsoft `TypeNameHandling`) | Trung bình |
| Cloud chạy API | Chưa xác định (có cả chuỗi AWS lẫn Azure, có thể từ thư viện) | Thấp |
| Loyalty / đổi thưởng thật | `hosted.myvip.co/rewardstore/` | Cao |

## 8. SDK bên thứ ba

| Nhóm | SDK |
|---|---|
| IAP / thanh toán | Unity IAP (+ UDP), Xsolla, AppCharge |
| Ads | Unity LevelPlay (ironSource), AppLovin, Moloco, Fyber, Meta Audience Network |
| Analytics / attribution | Firebase Analytics, AWS Kinesis Firehose + Cognito, Unity Analytics, Singular 12.5.5, Adjust, Braze |
| Crash | Firebase Crashlytics, AppLovin native crash reporter |
| Social | Facebook SDK |
| Khác | Helpshift, Unity Mobile Notifications, Google Play Review / Core |

## 9. Lớp native Android (Java/Kotlin) — từ AndroidManifest + dex

> Nguồn: string pool `AndroidManifest.xml` (parse AXML read-only) + type descriptor trong `classes.dex`–`classes4.dex` (~34 MB, 196 package cấp 3).

- **Chữ ký APK** (REA `inspect-apk-signature`): v1 + v2, file `META-INF/BNDLTOOL.*` → APK sinh từ **Android App Bundle (AAB)** bằng bundletool. Cert `CN=Andy Riedel, O=PlayStudios, L=Burlingame, CA`, tạo 2013-03-13, hạn 2063.
- **Entry point Unity override:** `com.playstudios.mykonami.override.MyKonamiActivity` / `MyKonamiApplication`.
- **Proxy activity riêng:** `androidProxies.PaymentsProxyActivity`, `SocialAuthProxyActivity`, `XsollaWidgetAuthProxyActivity`.
- **SDK native của PlayStudios:**
  - `com.playstudios.playlinksdk` — PlayLink SDK, kiến trúc clean (`system/data`, `system/domain_logic/{authentication,attribution,appcharge,coupon,ecommerce,identity,my_vip,rewards,user_consent…}`, `system/services/{bi,cookie_jar,device_information}`); trong dex có **Ktor client** + Kotlin → khả năng cao PlayLink viết bằng Kotlin dùng Ktor.
  - `com.playstudios.rewardsstore_sdk` — WebView host cho store đổi thưởng thật (myVIP).
  - `com.mykonami.browserbridge` — bridge mở browser/web.
- **WebView trong Unity:** `net.gree.unitywebview` (unity-webview).
- **JS engine nhúng:** `libquack.so` + `com.koushikdutta.quack` (Quack — chạy JavaScript trong Java); chưa rõ module nào dùng.
- **Ad mediation (rộng hơn đã liệt kê):** AppLovin MAX, ironSource/LevelPlay, Unity Ads, AdMob + SCAR, Meta Audience Network, Vungle/Liftoff, Fyber/DT FairBid, Moloco, HyprMX, Mintegral, Pangle (ByteDance), Chartboost, InMobi, Ogury, BidMachine, Smaato, myTarget, Yandex, Maio, Five, Verve (PubNative), Digital Turbine, Tapjoy, Amazon APS, Tencent GDT (`com.qq.e`).
- **Analytics / CRM bổ sung:** **mParticle** (customer data platform), Braze (push + in-app), Adjust, Singular (preinstall), Facebook App Events.
- **Store / push khác:** Google Play Billing + **Amazon Device Messaging** (`RECEIVE_ADM_MESSAGE`) → có bản cho Amazon Appstore; `com.outlinegames.unibill` (Unibill — IAP cũ, còn sót).
- **Permission đáng chú ý:** `ACCESS_FINE/COARSE_LOCATION` (có lẽ cho kiểm tra vùng đổi thưởng/sweepstakes — suy luận), `READ_PHONE_STATE`, `GET_ACCOUNTS`, Privacy Sandbox (`ACCESS_ADSERVICES_*`), `POST_NOTIFICATIONS`, `FOREGROUND_SERVICE_DATA_SYNC`. `INSTALL_PACKAGES`/`DUMP` là quyền hệ thống, app thường không được cấp (nhiều khả năng do SDK khai báo).

## 10. Đính chính so với phân tích ban đầu (grep string)

- `UnityRequestQueue` / `UnityWebRequestWrapper` thuộc **AWSSDK.Core**, không phải code game; queue thật là `NetworkManager.SimpleQueue`.
- `RemoteCommander` chỉ có `Command` / `Call` → có vẻ là tool debug/QA, không phải transport chính.
- Không có DOTween; tween là GoKit + tự viết.

## Câu hỏi chưa giải quyết

- Format request/response cụ thể (body JSON, ký request) — cần decompile thân method. REA chỉ link method address cho `libil2cpp.so` **64-bit**; bản XAPK này chỉ có ARMv7 (32-bit) → bị từ chối (`unsupported_target`). Cần bản `arm64-v8a` + Ghidra/IDA.
- Module nào dùng Quack (JS engine).
- Server chạy IIS hay Kestrel, cloud nào — chỉ biết qua response header từ production (chưa kiểm tra, cần cân nhắc về quyền).
- Script Lua có được ký / kiểm tra integrity không.
