# DataForge.Core

> 状态：活跃开发 | 技术栈：.NET 8（net8.0）轻量级 ETL / 数据管道库

## 概述

自有项目代码仓库，当前作为本地活跃开发分支使用。存在未推送至远程的本地提交。

本仓库提供面向 .NET 的声明式数据管道能力：统一的数据源接入、链式转换、验证与导出（CSV / JSON 等内置于核心包；真实 Excel 等能力通过扩展包按需引入）。详细用法见 [`DataForge.Core/README.md`](DataForge.Core/README.md)。

## 当前状态

- **保留为活跃开发分支**
- 本地有未推送提交（约 716KB 代码量）
- 远程：https://github.com/kongliuli/DataForge.Core
- 目标框架：`net8.0`；许可证：Apache-2.0

## 快速验证

在 `DataForge.Core/` 目录下：

```powershell
dotnet restore DataForge.Core.sln
dotnet build DataForge.Core.sln -c Release --no-restore
dotnet test  DataForge.Core.sln -c Release --no-build
```

## 待办

- [ ] 推送本地提交至远程，避免代码丢失
- [ ] 建立定期同步机制

## 说明

本目录位于 `0300-克隆仓库` 下，但实际为 **自有项目的活跃开发分支**，并非单纯的远程镜像。请勿将其与其他克隆仓库同等对待。
