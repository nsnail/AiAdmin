# build

代码清理、质量规则和版本标签脚本，以及共用的 MSBuild 配置。

| 文件                        | 说明                                                                                                              |
|-----------------------------|-------------------------------------------------------------------------------------------------------------------|
| `code.clean.full.ps1`       | 提交前必须运行的总入口，依次执行下面三个清理脚本                                                                  |
| `code.clean.prettier.ps1`   | 对变更的前端文件执行 Prettier 格式化；编译前端代码前也必须运行                                                    |
| `code.clean.resharper.ps1`  | 对 Git 中已修改、新增的文件执行 `jb cleanupcode`（需要安装 `jetbrains.resharper.globaltools`）                    |
| `code.clean.dot.ps1`        | 使用 `dot` 工具去除 BOM、行尾空白并统一换行为 LF                                                                  |
| `code.quality.props`        | 后端项目共用的静态分析配置：StyleCop、Roslynator、Sonar、线程与异步分析器，警告一律视为错误                       |
| `stylecop.analyzers.ruleset` | StyleCop 规则集，在 `code.quality.props` 中引用                                                                  |
| `new.version.tag.ps1`       | 交互式升级语义化版本标签 `vX.Y.Z`，校验本地与远程后创建注释标签并仅推送标签                                       |
| `sync.slnx.files.csx`       | 同步 `AiAdmin.slnx` 中 `/root/` 文件夹下的根目录文件列表                                                          |

## 使用

```powershell
./build/code.clean.full.ps1     # 提交前
./build/code.clean.prettier.ps1 # 编译前端前
```

## 说明

- 清理脚本会直接修改工作区文件，包括格式、换行，可能连带改动 `package.json` 的换行或依赖版本，提交前用 `git diff` 确认改动范围，不属于本次任务的改动应单独处理
- `code.quality.props` 把所有警告视为错误，新增代码必须 0 警告通过 `dotnet build`；需要抑制规则时优先局部抑制并写明原因，不要放宽整个项目
- 三个后端项目和测试项目都导入 `code.quality.props`，修改共用配置后需确认整个解决方案（含测试项目）仍能 0 警告编译
- `Directory.Build.props`（仓库根目录）决定输出目录：所有项目的中间文件与产物输出到仓库根目录的 `dist/` 下，文件名大小写不可更改