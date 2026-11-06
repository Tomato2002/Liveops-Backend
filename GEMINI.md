# PROJECT CODING RULES & TECH STACK STANDARDS
# Architecture: PLAYSTUDIOS / myKONAMI Stack (Unity + Lua MoonSharp + ASP.NET Core)

> **QUY TẮC TỐI THƯỢNG (MANDATORY GATEWAY):**
> File [PROJECT_CONTEXT.md](file:///e:/spin/PROJECT_CONTEXT.md) là **Single Source of Truth (SSOT)** của toàn bộ dự án.
> **BẤT KỲ HÀNH ĐỘNG NÀO** (tạo file, sửa code, refactor, thiết kế API, viết Lua script) **BẮT BUỘC PHẢI ĐỐI CHIẾU VỚI [PROJECT_CONTEXT.md](file:///e:/spin/PROJECT_CONTEXT.md) VÀ VƯỢT QUA 4 CỔNG KIỂM SOÁT (CHECKLIST)** trong file đó trước khi thực hiện.

Bạn là kỹ sư phần mềm cao cấp (Senior/Principal Game & Backend Engineer). Khi tạo, sửa đổi hoặc sinh mã nguồn trong workspace này, bạn PHẢI tuân thủ nghiêm ngặt 100% các quy tắc kiến trúc và tiêu chuẩn kỹ thuật dưới đây.

---

## 1. TỔNG QUAN KIẾN TRÚC & PHÂN TÁCH TRÁCH NHIỆM

### 1.1. Phân định Client - Server (Server-Authoritative)
- **Tất cả quyết định về tiền bạc, kết quả quay (RNG), bảng trả thưởng (Paytable), số dư ví (Balance) PHẢI nằm ở Backend.**
- **Client (Unity) KHÔNG BAO GIỜ tự sinh kết quả ngẫu nhiên hay tự cộng tiền.** Client chỉ gửi yêu cầu cược (Wager) và nhận kết quả để "trình diễn" (presentation).

### 1.2. Kiến trúc 4 tầng Client (Unity)
Code phía Unity PHẢI tuân thủ cấu trúc 4 tầng phân tách:
1. **Tầng 1 - App Shell:** 
   - `Bootstrap`: Điểm vào duy nhất, khởi động tuần tự (Init -> Network/Auth -> TimeSync -> Asset/Config -> Lobby/Game).
   - `NetworkManager`: Quản lý hàng đợi request (`QueueTransaction`), tự gắn header (`X-Client-Version`, `X-Device-ID`, `X-Request-Id`), tự động phân tích `CommonDto` bắn event toàn app (`OnBalanceUpdated`).
   - `GlobalEventManager`: Hàng đợi popup ưu tiên (Priority Popup Queue), tuyệt đối không để dialog/popup mở đè lên nhau.
2. **Tầng 2 - Meta / LiveOps Features:**
   - Mỗi tính năng (Shop, PrizePass, PiggyBank, Quests) là một module độc lập theo khuôn mẫu: `*ConfigLoader` + `*NetworkController` + `*BundleLoader` + `*DialogLoader`.
   - Có thể bật/tắt động từ xa mà không ảnh hưởng gameplay chính.
3. **Tầng 3 - Slot Engine (GameEngine2/GEL):**
   - Áp dụng **Strategy Pattern** và **Data-Driven bằng JSON**.
   - Phân rã máy slot thành các Controller độc lập: `StageController`, `StrategyController`, `SequenceController`, `WagerController`, `NetworkController`, `GameModel`.
   - Mỗi hành vi (quay cuộn, tính win, tính năng bonus) là một Strategy tách biệt, cấu hình qua JSON.
4. **Tầng 4 - Lua Bridge & Sandbox:**
   - Dùng **MoonSharp** (C# Lua interpreter).
   - **Sandbox Proxy:** Lua KHÔNG ĐƯỢC truy cập trực tiếp `UnityEngine.GameObject`. Bắt buộc phải thông qua Proxy an toàn (`LuaGameObjectProxy`) để bảo vệ luồng dữ liệu và giảm rủi ro crash/leak.

---

## 2. QUY CHUẨN CODE C# UNITY (HIỆU NĂNG & ZERO-ALLOC GC)

### 2.1. Quản lý Bất đồng bộ (Async)
- **BẮT BUỘC dùng `UniTask`**, TUYỆT ĐỐI KHÔNG dùng `System.Threading.Tasks.Task` hay `Coroutine` truyền thống trong gameplay.
- **CancellationToken:** Mọi method async PHẢI nhận hoặc liên kết với `CancellationToken` (ví dụ: `this.GetCancellationTokenOnDestroy()`).
- **Tránh `async void`:** Chỉ dùng `UniTaskVoid` với `.Forget()` ở tầng ngoài cùng (Button OnClick handler) kèm khối `try/catch` đầy đủ. Các tầng nghiệp vụ bắt buộc trả về `UniTask` hoặc `UniTask<T>`.

### 2.2. Kiểm soát Rác bộ nhớ (GC Allocation)
- **TUYỆT ĐỐI KHÔNG dùng LINQ** trong vòng lặp `Update()`, `LateUpdate()`, hoặc các hàm gọi liên tục (Reel spinning, Rollup).
- **String Formatting:** Không dùng phép cộng chuỗi `+` hoặc `string.Format` trong UI update mỗi frame. Bắt buộc dùng `TMP_Text.SetText("{0}", value)` hoặc `StringBuilder` đã cache.
- **Object Pooling:** Mọi đối tượng sinh ra và hủy liên tục (Reel symbols, coin bay, particle, popup) BẮT BUỘC dùng `UnityEngine.Pool.ObjectPool<T>`. Khi release về pool PHẢI reset trạng thái sạch sẽ.

### 2.3. Animation, Tween & UI
- **Tween Lifetime:** Mọi Tween (DOTween/GoKit/PrimeTween) PHẢI gắn với vòng đời GameObject bằng `.SetLink(gameObject)` hoặc kill chủ động trong `OnDisable()`/`OnDestroy()`.
- **uGUI Optimization:**
  - Tách Canvas tĩnh (khung nền) và Canvas động (số coin nhảy, timer, cuộn quay) để tránh canvas rebuild toàn bộ.
  - Tắt `Raycast Target` trên tất cả Image và Text không cần tương tác click.
  - Dùng `RectMask2D` thay cho `Mask` trừ khi bắt buộc phải dùng Stencil.

---

## 3. QUY CHUẨN CODE BACKEND (C# / ASP.NET CORE)

### 3.1. Clean Architecture & Stateless Design
- **API Server PHẢI Stateless:** Tuyệt đối không lưu session, tiền, hay ván chơi của người chơi trong biến static / RAM của process.
- **Tầng Domain độc lập:**
  - `Domain`: Chứa Logic tính toán thuần túy (RNG, Paytable, Wallet rules). KHÔNG tham chiếu tới Entity Framework, Web API hay thư viện bên ngoài.
  - `Application`: Xử lý Use Case / Command / Query / DTOs.
  - `Infrastructure`: Xử lý Database (EF Core / Dapper), Redis Cache, Distributed Locks.
  - `Api`: Controllers, Middlewares, Filters.

### 3.2. An toàn Giao dịch & Concurrency (Wallet & Spin)
- **Chống Hack Double Spending:** Mọi thao tác thay đổi số dư ví (trừ cược, cộng thưởng) BẮT BUỘC sử dụng **Distributed Lock** (trên Redis) hoặc **Optimistic Concurrency Control** (phiên bản `RowVersion`).
- **Idempotency Key:** API Spin và Mua hàng BẮT BUỘC yêu cầu header `X-Request-Id`. Nếu request bị gửi trùng do lag mạng, server phải trả lại kết quả đã xử lý từ Cache thay vì quay lần 2.
- **Common DTO Pattern:** Mọi response trả về cho client cần đi kèm đối tượng `CommonDto` (chứa `Balance`, `Level`, `Exp` mới nhất) để client tự động đồng bộ UI.

---

## 4. QUY CHUẨN LUA (MOONSHARP SANDBOX)

- Tất cả script Lua phải tuân theo cấu trúc module rõ ràng (`local M = {}; return M`).
- Không tạo biến toàn cục (Global variable) bừa bãi trong Lua. Luôn dùng `local`.
- Tương tác với Unity luôn thông qua Proxy đăng ký sẵn:
  ```lua
  -- ĐÚNG:
  proxy:SetActive(true)
  proxy:PlayAnimation("Spin")
  
  -- SAI:
  CS.UnityEngine.GameObject.Find("...") -- Nghiêm cấm gọi trực tiếp Unity API không qua kiểm soát
  ```

---

## 5. NGUYÊN TẮC LÀM VIỆC CỦA AI TRONG WORKSPACE NÀY
1. **Không giả lập (No Mocking) nửa vời:** Khi viết class, hãy viết code hoàn chỉnh, logic thực tế, có xử lý lỗi và ngoại lệ.
2. **Comment giải thích lý do thiết kế:** Với các đoạn code xử lý concurrency, zero-alloc hoặc proxy bridge, hãy ghi chú rõ nguyên nhân để dễ bảo trì.
3. **Tuân thủ đúng tên file và cấu trúc thư mục đã quy định.**
