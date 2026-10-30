# .github/workflows

GitHub Actions 工作流目录，**目前没有工作流**。

项目的发布流水线运行在 Gitea 上，见 [.gitea/workflows](../../.gitea/workflows/README.md)。如果后续需要在 GitHub 上运行持续集成，可以在此新增工作流，建议至少包含：

- 后端：`dotnet build`（要求 0 警告 0 错误）与 `dotnet test tests/AiAdmin.Tests`
- 前端：`cnpm install` 与 `npx vite build`

新增工作流时同步更新本文件，并避免与 Gitea 流水线重复执行镜像发布。
