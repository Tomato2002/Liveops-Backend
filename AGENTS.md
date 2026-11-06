# PROJECT CODING RULES & TECH STACK STANDARDS
# Architecture: PLAYSTUDIOS / myKONAMI Stack (Unity + Lua MoonSharp + ASP.NET Core)

> **QUY TẮC TỐI THƯỢNG:** Mọi hành động BẮT BUỘC phải đọc và đối chiếu với [PROJECT_CONTEXT.md](file:///e:/spin/PROJECT_CONTEXT.md) và [GEMINI.md](file:///e:/spin/GEMINI.md) trước khi thực hiện.

Mọi mã nguồn sinh ra trong dự án này bắt buộc tuân theo:
1. Server-Authoritative Architecture (C# ASP.NET Core Web API + Redis Distributed Locks + Idempotency).
2. Clean Architecture cho Backend (Domain, Application, Infrastructure, Api).
3. 4-Layer Architecture cho Unity Client (App Shell, Meta Features, Slot Engine Strategy, Lua Bridge).
4. Zero-Alloc GC & UniTask chuẩn mực cho Unity.
5. MoonSharp Lua Sandbox qua Proxy Pattern (`LuaGameObjectProxy`).
