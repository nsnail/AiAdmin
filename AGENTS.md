# AiAdmin 工作区规则

## 整体规范
- 每次git commit前必须运行 `./build/code.clean.full.ps1`
- 每次运行$git-log-and-commit skill前必须运行 `./build/code.clean.full.ps1`

## 前端规范
- 依赖安装、脚本执行统一使用 `cnpm`，不要使用 `pnpm`。
- 所有表格列表页统一使用 `ArtTable + useTable` 方案
- 所有新增或修改的用户界面文案必须接入 i18n，支持中英文切换
- 所有表格包含 `IsEnabled` 字段时，必须使用 `Switch` 开关列展示和操作
- 每次编译代码前必须运行 `./build/code.clean.prettier.ps1`

## 后端规范
- 每次编译代码前必须运行 `./build/code.clean.resharper.ps1`
- 正式版发布前（仓库没有 `v1` 及以上版本 tag）不考虑旧版本兼容、数据迁移或历史数据回填，按当前模型直接开发。
- 新增或修改代码必须补充清晰的中文注释，说明文件职责以及关键类和方法的作用。
- 每个类或record必须单独放在一个 `.cs` 文件中。
- 类、方法和属性必须使用标准 .NET XML 文档注释；方法参数必须使用 `<param>` 说明，返回值必须使用 `<returns>` 说明。
- XML 文档注释的中文说明结尾不写句号。
- XML 文档注释中，`<summary>` 的内容必须独占一行；`<param>`、`<returns>`、`<typeparam>` 等其他节点的内容与标签写在同一行。
- `.cs` 文件中的字符串字面量不得包含中文，统一使用英文