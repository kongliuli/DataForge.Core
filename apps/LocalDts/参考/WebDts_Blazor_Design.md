# WebDts (Blazor) 现代化数据迁移平台设计方案

本方案旨在将 `LocalDts` 数据迁移工具的核心能力扩展到 Web 平台，通过 **Blazor** 技术栈构建一个名为 `WebDts` 的现代化数据迁移平台。本报告将详细阐述 Blazor 两种托管模型（Server 和 WebAssembly）的选型考量、前后端架构设计、核心组件复用策略以及 Web 环境下的安全性增强措施。

---

## 1. Blazor 托管模型选型分析

Blazor 提供了两种主要的托管模型：Blazor Server 和 Blazor WebAssembly。针对数据迁移工具的特性，我们需要权衡它们的优劣。

| 特性 | Blazor Server | Blazor WebAssembly | 选型考量 (数据迁移) |
| :--- | :--- | :--- | :--- |
| **执行位置** | 服务器端 | 客户端浏览器 | 数据迁移涉及大量 IO 和计算，服务器端执行更安全高效。 |
| **初始加载** | 快 | 慢 (下载 .NET 运行时) | 对于工具类应用，初始加载速度不是首要瓶量，但 WebAssembly 可离线运行。 |
| **网络延迟** | 依赖 WebSocket，高延迟影响体验 | 不依赖 WebSocket，低延迟 | 迁移过程实时反馈需要低延迟，但主要计算在服务器。 |
| **文件系统访问** | 完全访问服务器文件系统 | 沙盒环境，有限访问 | **关键**：数据源/目标源可能在服务器或客户端，需灵活处理。 |
| **数据库连接** | 直接连接服务器可访问的数据库 | 无法直接连接 | **关键**：客户端无法直连数据库，必须通过 API。 |
| **安全性** | 业务逻辑在服务器，更安全 | 业务逻辑暴露在客户端 | 敏感数据和逻辑应始终在服务器端处理。 |
| **可扩展性** | 易于集成现有 .NET 后端服务 | 需 API 调用后端服务 | `LocalDts.Core` 可直接复用，Blazor Server 更直接。 |
| **离线能力** | 无 | 有 | 数据迁移通常需要在线，离线能力非核心需求。 |

**结论**：考虑到数据迁移的**安全性、性能要求（大量 IO 和计算）以及对服务器文件系统和数据库的直接访问需求**，**Blazor Server** 是更适合 `WebDts` 的首选托管模型。它允许 `LocalDts.Core` 模块直接在服务器端运行，并利用 SignalR 实现 UI 与服务器的实时通信，提供与桌面应用相近的响应速度。同时，可以结合 WebAssembly 作为轻量级前端，通过 API 调用后端服务。

---

## 2. WebDts 架构设计

`WebDts` 将采用典型的 **Client-Server 架构**，其中 Blazor Server 应用作为前端 UI 和后端业务逻辑的桥梁。

### 2.1 整体架构图

```mermaid
graph TD
    subgraph Client (浏览器)
        A[Blazor UI] -- SignalR --> B(Blazor Server Host)
    end

    subgraph Server (.NET 8)
        B -- HTTP/gRPC --> C(WebDts API)
        C -- DI --> D(LocalDts.Core)
        D -- Plugin Loading --> E(Plugins)
        D -- DB Access --> F(Databases)
        D -- File Access --> G(File System)
    end

    subgraph External Systems
        F -- Data --> H(Source/Target DBs)
        G -- Files --> I(Source/Target Files)
    end

    A -- User Interaction --> B
    B -- UI Updates --> A
    C -- Data Transfer --> D
    D -- Data Migration --> F & G
```

### 2.2 核心组件复用与改造

`LocalDts` 的模块化设计使得核心逻辑可以高效复用。

| `LocalDts` 模块 | `WebDts` 复用策略 | 改造点 |
| :--- | :--- | :--- |
| **DataMigration.Contracts** | **完全复用** | 作为前后端数据传输的契约，无需修改。 |
| **DataMigration.Core** | **核心复用** | 迁移引擎、插件管理、数据记录池等核心逻辑直接复用。 | 需要将 `SQLiteHelper` 等直接文件操作封装为服务，通过 API 暴露。 |
| **Plugins** | **部分复用** | 大部分数据源/目标源/转换器插件可直接复用。 | 涉及本地文件路径选择的插件（如 CSV, Excel）需要调整为支持上传或服务器路径。 |
| **DataMigration.Wpf** | **替换** | WPF UI 将被 Blazor UI 完全取代。 | - |
| **DataMigration.Console** | **保留/扩展** | 可作为后台任务或自动化脚本的入口。 | 可通过 API 触发 Console 任务。 |

### 2.3 前端 (Blazor UI) 设计

*   **UI 框架**：可以继续使用 `HandyControl` 的 Blazor 版本（如果可用）或选择其他流行的 Blazor UI 组件库，如 `MudBlazor`、`Ant Design Blazor` 等，以保持现代化风格。
*   **导航**：采用侧边栏导航 + 顶部步骤条的布局，与 WPF 优化方案保持一致。
*   **实时反馈**：利用 Blazor Server 的 SignalR 连接，实现迁移进度、日志的实时推送，提供流畅的用户体验。
*   **文件上传**：对于 CSV、Excel 等文件型数据源，前端需要提供文件上传功能，将文件传输到服务器端进行处理。

---

## 3. Web 环境下的插件加载与安全性

### 3.1 插件加载策略

*   **服务器端加载**：所有插件都应部署在 Blazor Server 应用的服务器端。`IPluginManager` 负责在服务器启动时加载插件。
*   **插件配置**：插件的配置信息（如连接字符串、文件路径）通过 Web UI 收集，并通过 API 传递给服务器端的插件实例。

### 3.2 安全性增强

*   **认证与授权**：引入用户认证（如 ASP.NET Core Identity）和基于角色的授权机制，确保只有授权用户才能执行数据迁移任务。
*   **数据加密**：敏感配置信息（如数据库凭据）在传输和存储时必须加密。传输使用 HTTPS，存储使用服务器端的加密机制（如 `ProtectedData` 或密钥管理服务）。
*   **文件访问控制**：严格限制服务器端文件系统的访问权限。用户上传的文件应存储在沙盒目录中，并进行病毒扫描。
*   **API 安全**：所有与后端交互的 API 都应进行输入验证、速率限制和跨站请求伪造 (CSRF) 保护。
*   **日志审计**：详细记录所有用户操作和迁移任务的执行日志，便于审计和问题追溯。

---

## 4. 总结

将 `LocalDts` 迁移到 Blazor 平台，构建 `WebDts`，将使其从一个桌面工具升级为一个**更具普适性、易于部署和访问**的企业级数据迁移解决方案。Blazor Server 模型能够最大化地复用现有 .NET 核心逻辑，同时提供强大的实时交互能力。通过遵循上述架构和安全设计原则，`WebDts` 将能够提供一个安全、高效且用户友好的数据迁移体验。

---
**设计人**: Manus AI  
**日期**: 2026年4月3日

## 5. 参考文献

[1] Microsoft Docs. (n.d.). *ASP.NET Core Blazor hosting models*. Retrieved from [https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models)
