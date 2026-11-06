# Bộ 30 câu hỏi phỏng vấn Unity Middle — theo stack myKONAMI (PLAYSTUDIOS)

> Stack suy ra từ APK myKONAMI 1.140.0: Unity 6000.0.73f1, IL2CPP, Built-in RP, uGUI + TextMeshPro, UniTask, GoKit + tween tự viết (không có DOTween), Spine, MoonSharp (Lua), Ninject, Utf8Json, UnityWebRequest, AssetBundle + CDN Akamai, Unity IAP, LevelPlay/AppLovin, Firebase. Backend: ASP.NET Web API (C#).
> Chi tiết: [mykonami-tech-stack-analysis.md](mykonami-tech-stack-analysis.md).
> Mỗi câu có: **đáp án mẫu** (ý chính cần nói) + **điểm cộng** (phân biệt Middle với Junior).

---

## A. C# & Async (1–6)

### 1. Value type vs reference type? Boxing là gì, khi nào xảy ra trong Unity?
**Đáp án:** `struct` lưu giá trị (stack hoặc inline trong object chứa nó), `class` lưu reference tới heap. Boxing = bọc value type thành `object` trên heap → tạo GC allocation. Hay gặp: truyền `int` vào `params object[]` (`string.Format`), dùng `enum` làm key `Dictionary` (trên runtime cũ), gọi method interface trên struct qua biến kiểu interface, `ArrayList`.
**Điểm cộng:** dùng generic constraint (`where T : struct`) hoặc `IEquatable<T>` để tránh boxing; kiểm tra bằng Profiler (GC Alloc column).

### 2. Những nguồn GC allocation phổ biến trong `Update()` và cách tránh?
**Đáp án:** string concat / `ToString()` mỗi frame; LINQ; closure/lambda capture biến; `new List<>` mỗi frame; `GetComponent` trả mảng (`GetComponents`); `foreach` trên một số collection cũ; `Camera.main` (Unity cũ). Cách tránh: cache, tái sử dụng list (`list.Clear()`), `StringBuilder` hoặc chỉ update text khi giá trị đổi, object pool, API non-alloc (`Physics.RaycastNonAlloc`).
**Điểm cộng:** với UI slot: chỉ set `TMP_Text.text` khi số coin thay đổi, dùng `SetText("{0}", value)` của TMP để tránh alloc.

### 3. Coroutine vs async/await vs UniTask — khác nhau thế nào?
**Đáp án:**
- Coroutine: chạy trên main thread, gắn với lifetime MonoBehaviour, không trả về giá trị, khó bắt exception, mỗi `yield new WaitForSeconds` alloc.
- `Task` (.NET): có giá trị trả về và exception, nhưng nặng (alloc), không biết PlayerLoop của Unity, không tự dừng khi thoát Play Mode.
- **UniTask**: struct-based, gần như zero-alloc, chạy theo PlayerLoop (`UniTask.Yield`, `NextFrame`, `Delay`), hỗ trợ `CancellationToken`, await được `UnityWebRequest`, `AsyncOperation`, DOTween.
**Điểm cộng:** nói được UniTask không được await 2 lần (giống `ValueTask`), dùng `.Preserve()` nếu cần.

### 4. Hủy (cancel) một tác vụ async khi GameObject bị destroy như thế nào?
**Đáp án:** truyền `CancellationToken` xuống mọi hàm async; với UniTask dùng `this.GetCancellationTokenOnDestroy()` (hoặc `destroyCancellationToken` built-in Unity 2022.2+). Kết hợp nhiều nguồn bằng `CancellationTokenSource.CreateLinkedTokenSource`.
```csharp
async UniTaskVoid LoadAvatar(string url)
{
    var ct = this.GetCancellationTokenOnDestroy();
    using var req = UnityWebRequestTexture.GetTexture(url);
    await req.SendWebRequest().WithCancellation(ct);
    avatar.texture = DownloadHandlerTexture.GetContent(req);
}
```
**Điểm cộng:** nếu không cancel → callback chạy trên object đã destroy → `MissingReferenceException`.

### 5. `async void` vs `async UniTaskVoid` vs `.Forget()` — rủi ro gì?
**Đáp án:** `async void` (và fire-and-forget) không ai await → exception có thể bị nuốt hoặc crash khó debug. UniTask: dùng `UniTaskVoid` hoặc `.Forget()`, exception được đẩy về `UniTaskScheduler.UnobservedTaskException` (log ra Console). Chỉ dùng fire-and-forget ở "điểm vào" (event handler của button), còn lại luôn trả `UniTask` để caller await.
**Điểm cộng:** wrap `try/catch` ở điểm vào, log lên Crashlytics.

### 6. Event/delegate: lỗi memory leak hay gặp?
**Đáp án:** subscribe event của một object sống lâu (singleton, static event) từ object sống ngắn (UI popup) nhưng quên unsubscribe → popup không được GC, callback gọi vào object đã destroy. Quy tắc: subscribe trong `OnEnable`, unsubscribe trong `OnDisable` (hoặc `OnDestroy`).
**Điểm cộng:** nhắc lambda không unsubscribe được nếu không giữ reference.

---

## B. Unity core & Performance (7–13)

### 7. Thứ tự lifecycle và lỗi hay gặp khi phụ thuộc giữa các script?
**Đáp án:** `Awake` → `OnEnable` → `Start` → `FixedUpdate` → `Update` → `LateUpdate` → `OnDisable` → `OnDestroy`. `Awake` chạy cả khi component disabled (GameObject active), `Start` chỉ chạy khi enabled. Quy tắc: `Awake` khởi tạo bản thân, `Start` lấy reference sang object khác. Thứ tự giữa các script khác nhau không đảm bảo → dùng Script Execution Order hoặc tốt hơn: một bootstrap/composition root khởi tạo theo thứ tự rõ ràng (đây là chỗ DI như Ninject phát huy).

### 8. Tối ưu uGUI cho màn hình có nhiều element động (lobby slot, số coin chạy)?
**Đáp án:**
- Thay đổi 1 element → **cả Canvas rebuild**. Tách Canvas tĩnh và động (nested Canvas cho phần hay đổi như counter, timer).
- Tắt `Raycast Target` trên Image/Text không cần click.
- Hạn chế Layout Group lồng nhau, đặc biệt trên item cuộn; tính layout 1 lần rồi tắt.
- Dùng `RectMask2D` thay `Mask` (Mask tốn stencil và phá batching).
- Ẩn UI bằng `CanvasGroup.alpha = 0` + tắt raycast, hoặc tắt component Canvas thay vì `SetActive` liên tục (SetActive gây rebuild/alloc).
- ScrollView nhiều item → recycle (virtualized list).
**Điểm cộng:** đo bằng Profiler module UI (`Canvas.BuildBatch`, `Canvas.SendWillRenderCanvases`).

### 9. Draw call, batching, Sprite Atlas — giải thích và cách giảm?
**Đáp án:** mỗi lần đổi material/texture → batch mới. uGUI batch các element cùng material + texture liền nhau theo thứ tự hierarchy (không bị element khác material chen giữa). Giảm bằng: Sprite Atlas gom sprite cùng màn hình, dùng chung material, TMP font atlas chung, tránh xen kẽ Text và Image khác atlas. Debug bằng **Frame Debugger** xem lý do batch bị break.

### 10. Object pooling — khi nào cần, thiết kế thế nào?
**Đáp án:** khi spawn/destroy liên tục (coin bay, particle win, symbol trên reel). `Instantiate/Destroy` tốn CPU và gây GC. Unity có sẵn `UnityEngine.Pool.ObjectPool<T>`: `Get`, `Release`, callback `actionOnGet/actionOnRelease` để reset state. Lưu ý: reset đầy đủ (DOTween tween đang chạy phải `Kill`, Spine animation về state đầu), giới hạn `maxSize`, prewarm khi loading.

### 11. Reel slot quay: làm sao để reel dừng đúng symbol server trả về?
**Đáp án:** reel là danh sách symbol view cuộn vòng (recycle khi ra khỏi mask). Khi bắt đầu spin: quay "vô hạn" với tốc độ cố định trong lúc chờ response. Khi có kết quả: tính quãng đường còn lại sao cho symbol đích dừng đúng row, chèn symbol đích vào "đường ray" phía trên, chạy tween giảm tốc (`Ease.OutBack` cho cảm giác nảy). Các reel dừng lệch nhau vài trăm ms. Nếu response lỗi/timeout → dừng reel về trạng thái cũ, hoàn tiền hiển thị, báo lỗi.
**Điểm cộng:** tách **logic kết quả** (data) khỏi **trình diễn** (view); không bao giờ để client tự random kết quả.

### 12. Tween library (DOTween/GoKit/PrimeTween): những lỗi hay gặp?
> myKONAMI dùng GoKit + tween tự viết (`playstudios.components`), nhưng các lỗi dưới đây giống nhau ở mọi tween library; ví dụ minh họa bằng DOTween vì phổ biến nhất.

**Đáp án:** tween vẫn chạy khi object đã destroy → lỗi null; tween chồng lên nhau trên cùng property. Cách xử lý: `SetLink(gameObject)` hoặc `DOKill()` trong `OnDisable`, `SetId` để kill theo nhóm, dùng `Sequence` cho chuỗi animation. Với pool: `Kill` tween khi release. Dùng `SetUpdate(true)` nếu cần chạy khi `Time.timeScale = 0`. Có thể await tween bằng UniTask (`await tween.ToUniTask(cancellationToken: ct)`).

### 13. Bạn profile một máy Android yếu bị giật ở lobby, quy trình thế nào?
**Đáp án:** build Development + Autoconnect Profiler, profile **trên device thật** (không tin Editor). Xem CPU: Main thread bị gì (scripts, UI rebuild, GC.Collect, loading). Xem GC Alloc mỗi frame. Memory Profiler: texture quá lớn, bundle không unload. Frame Debugger: số draw call, overdraw. Sửa từ chỗ tốn nhất, đo lại sau mỗi thay đổi.
**Điểm cộng:** nhắc incremental GC, `Application.targetFrameRate`, overdraw do UI full-screen trong suốt chồng nhau.

---

## C. IL2CPP & Serialization (14–17)

### 14. IL2CPP vs Mono? Hạn chế của IL2CPP?
**Đáp án:** IL2CPP chuyển IL → C++ → native (AOT): nhanh hơn, khó reverse hơn, bắt buộc với iOS và 64-bit Google Play. Hạn chế: không tạo code lúc runtime (`System.Reflection.Emit`, generic instantiate mới với value type chưa có lúc build có thể lỗi), build chậm hơn, code bị strip.

### 15. Code stripping là gì, `link.xml` dùng để làm gì?
**Đáp án:** Managed Stripping xóa class/method "không thấy được dùng" để giảm size. Code chỉ được gọi qua reflection (JSON deserialize, DI container tạo object qua constructor) có thể bị xóa → crash runtime trên build nhưng chạy tốt trong Editor. Giữ lại bằng `link.xml` hoặc attribute `[Preserve]`.
**Điểm cộng:** lỗi kiểu này chỉ lộ ra trên device → luôn test build release trước khi ship.

### 16. Vì sao game kiểu này dùng Utf8Json/formatter sinh sẵn thay vì `JsonUtility` hay Newtonsoft thuần?
**Đáp án:**
- `JsonUtility`: nhanh nhưng không hỗ trợ `Dictionary`, property, polymorphism, nullable.
- Newtonsoft: linh hoạt nhưng dựa nhiều vào reflection → chậm, alloc nhiều, dễ lỗi stripping/AOT trên IL2CPP.
- Utf8Json/MessagePack với **formatter sinh sẵn (code generation)**: không reflection lúc runtime, ít alloc, an toàn với AOT. Phù hợp app gọi API liên tục với payload lớn (config, shop, live event).

### 17. Thay đổi model JSON giữa client và server mà không làm hỏng client cũ?
**Đáp án:** chỉ **thêm** field mới (optional, có default), không đổi tên/xóa field đang dùng; client bỏ qua field lạ; dùng version API (`/v2/...`) khi phải thay đổi phá vỡ; server có thể ép update (force update) khi client quá cũ.

---

## D. Networking (18–24)

### 18. Thiết kế lớp network client cho game client–server (server quyết định kết quả)?
**Đáp án:** chia tầng:
1. **Transport**: wrapper `UnityWebRequest` (timeout, header, gzip, cancel).
2. **Command/Request**: mỗi API là một object request có model in/out, gửi qua queue.
3. **Error handling**: phân loại lỗi (mạng, timeout, 4xx, 5xx, session hết hạn, maintenance) → strategy tương ứng.
4. **Service/Controller** theo feature (`ShopApiController`, `DailyBonusApiController`) gọi command, cập nhật state.
UI không bao giờ gọi HTTP trực tiếp.
**Điểm cộng:** inject qua interface (`INetworkManager`) để mock trong test.
**Liên hệ myKONAMI:** `NetworkManager` (`QueueTransaction` → `SimpleQueue` → `ProcessRequestQueue`, `ApplyDynamicHeaders`, `Recover`, `RestartSession`, `LoadDynamicEndpoints`) + ~35 controller theo feature (`ShopController`, `LobbyController`, `DailyBonusController`…) + `SpinNetworkStrategy` cho slot engine.

### 19. Retry thế nào cho đúng? Request nào được retry, request nào không?
**Đáp án:** chỉ retry lỗi tạm thời (timeout, mất mạng, 5xx, 429), không retry 4xx (lỗi dữ liệu). Dùng **exponential backoff + jitter** (1s, 2s, 4s… + random) để không dồn tải lên server. Request có side effect (spin, mua hàng, nhận thưởng) chỉ retry an toàn nếu có **idempotency key/request id**: server thấy id trùng thì trả lại kết quả cũ thay vì trừ tiền lần 2.

### 20. Mất mạng giữa lúc spin (đã gửi request, chưa nhận response) — xử lý thế nào?
**Đáp án:** client không biết server đã xử lý hay chưa. Gửi lại **cùng request id** → server trả kết quả đã có (idempotent). Nếu vẫn lỗi: hiện popup "mất kết nối", khi kết nối lại thì gọi API lấy state/balance mới nhất từ server và đồng bộ lại UI. Balance hiển thị luôn lấy theo server (client chỉ trừ tạm để có cảm giác nhanh).

### 21. Làm sao chống người chơi chỉnh giờ điện thoại để nhận bonus theo giờ sớm hơn?
**Đáp án:** không tin giờ máy. Khi login/heartbeat, server trả `serverTime`; client lưu `delta = serverTime - localTimeAtReceive` và dùng đồng hồ đơn điệu (`Time.realtimeSinceStartup`) để tính thời gian trôi. Timer hiển thị = server time ước lượng. Quan trọng nhất: **server kiểm tra lại** khi nhận thưởng — client chỉ hiển thị.

### 22. Heartbeat/polling vs WebSocket — khi nào chọn cái nào?
**Đáp án:**
- Polling/heartbeat (HTTP định kỳ): đơn giản, dễ scale, qua mọi proxy, đủ dùng khi dữ liệu không cần realtime từng giây (jackpot, live event, giữ session). Đây là cách myKONAMI có vẻ đang làm.
- WebSocket: cần khi server phải đẩy ngay (chat, PvP, tournament realtime); phức tạp hơn: reconnect, giữ kết nối khi app background, scale server.
**Điểm cộng:** khi app vào background → dừng heartbeat; khi resume → refresh state ngay.

### 23. Session/token hết hạn khi đang có nhiều request đang chạy?
**Đáp án:** khi nhận 401: tạm dừng queue, chỉ **một** lần refresh token (các request khác chờ cùng một task refresh), refresh xong thì gửi lại các request đang chờ. Nếu refresh thất bại → đưa về màn login. Tránh mỗi request tự refresh riêng (race condition, gọi refresh hàng loạt).

### 24. Bảo mật phía client: những gì nên và không nên làm?
**Đáp án:** client luôn có thể bị đọc/sửa (dump IL2CPP, proxy). Vì vậy: logic quan trọng (kết quả, tiền, phần thưởng) ở server; dùng HTTPS (có thể thêm certificate pinning); không hardcode secret quan trọng; ký request hoặc dùng token ngắn hạn; validate receipt IAP ở server. Obfuscation chỉ làm khó, không thay được server-authoritative.

---

## E. Asset delivery (25–27)

### 25. AssetBundle vs Addressables — khác nhau, khi nào tự viết hệ thống riêng?
**Đáp án:** Addressables là lớp trên AssetBundle: quản lý dependency, reference counting, catalog, load theo address, hỗ trợ remote content. Tự viết trên AssetBundle khi đã có hệ thống từ lâu (legacy) hoặc cần kiểm soát hoàn toàn (manifest riêng theo game/máy slot, CDN riêng, versioning riêng). Dù cách nào cũng phải giải quyết: dependency, version, cache, unload.

### 26. Thiết kế hệ thống tải nội dung theo yêu cầu cho hàng trăm máy slot?
**Đáp án:** server/CDN có **manifest** (danh sách bundle + hash/version + size + dependency). Lúc khởi động tải manifest mới; khi người chơi mở máy: kiểm tra cache theo hash → tải nếu thiếu/cũ (có progress, retry, timeout), tải dependency trước (shared bundle: font, UI chung). Lobby hiện trạng thái "cần tải" + dung lượng. Dọn cache máy lâu không chơi. Kiểm tra dung lượng trống trước khi tải.

### 27. Memory leak khi dùng AssetBundle?
**Đáp án:** `Unload(false)` chỉ unload bundle, asset đã load vẫn ở trong memory; `Unload(true)` unload cả asset (có thể làm mất texture đang hiển thị). Asset bị load trùng từ bundle đã unload rồi load lại → 2 bản trong memory. Cách làm: reference counting theo bundle, unload khi rời máy slot, gọi `Resources.UnloadUnusedAssets()` khi chuyển màn. Kiểm tra bằng Memory Profiler (so sánh snapshot trước/sau khi vào-ra máy).

---

## F. Kiến trúc & Lua (28–29)

### 28. Vì sao nhúng Lua (MoonSharp) vào Unity IL2CPP? Đánh đổi?
**Đáp án:** IL2CPP là AOT → không tải được code C# mới; Apple cấm tải native code. Lua là script được interpreter chạy → tải từ CDN để ra máy slot mới / hotfix **không cần update app**. MoonSharp là pure C# → chạy mọi nền tảng kể cả WebGL. Đánh đổi: chậm hơn, chi phí gọi qua lại C#↔Lua, debug khó, phải giữ API contract tương thích ngược. Phương án khác: HybridCLR (hot-update C#), data-driven config (JSON/ScriptableObject).
**Liên hệ myKONAMI:** có 2 thế hệ slot engine song song — `GameEngine2` (C#, máy cũ) và `PlayStudios.GameEngineLua` (GEL, máy mới, script Lua tải từ CDN qua `LuaGameScriptsManager.DownloadScript`). Cả hai chia hành vi máy slot thành hàng trăm Strategy (Strategy pattern); contract giữa app và máy slot là `IGameApi` (`Initialize`, `StartGame`, `DispatchEvent`, `DestroyGame`).

### 29. Dependency Injection (Ninject/Zenject/VContainer) mang lại gì? Khi nào không nên dùng?
**Đáp án:** lớp phụ thuộc vào interface, container cung cấp implementation → dễ thay thế, dễ test (mock `IGameApi`), giảm singleton/`FindObjectOfType`, khởi tạo có thứ tự rõ. Nhược điểm: khó lần theo luồng, chi phí reflection (Ninject khá nặng, VContainer nhẹ hơn), rủi ro stripping trên IL2CPP. Project nhỏ hoặc prototype không cần.

---

## G. IAP & LiveOps (30)

### 30. Luồng IAP an toàn từ lúc bấm mua đến lúc nhận coin? App bị kill giữa chừng thì sao?
**Đáp án:**
1. Client gọi Unity IAP → store xử lý thanh toán → trả receipt.
2. Client **không cộng coin**, gửi receipt lên server.
3. Server xác thực với Google/Apple, chống dùng lại receipt (lưu transaction id), cộng coin, trả balance mới.
4. Client nhận OK mới gọi `ConfirmPendingPurchase` (trả `PurchaseProcessingResult.Pending` trong `ProcessPurchase` cho tới lúc đó).
App bị kill sau bước 1: giao dịch vẫn ở trạng thái pending; lần mở app sau Unity IAP gọi lại `ProcessPurchase` → client gửi lại lên server (server idempotent theo transaction id) → không mất tiền, không cộng 2 lần.

---

## Câu hỏi nên hỏi lại người phỏng vấn
- Team chia theo feature hay theo layer? Có team riêng viết content (Lua) cho máy slot không?
- Tần suất release app và release content (máy mới) là bao lâu?
- Quy trình code review, test (unit test, QA), CI/CD build như thế nào?
- Đang có dự định chuyển sang URP/Addressables hay nâng version Unity không?
