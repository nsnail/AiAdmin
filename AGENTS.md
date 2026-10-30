# AiAdmin 工作区规则

本文件是 AiAdmin 框架的开发约定，适用于基于本框架构建的所有业务项目。具体业务的设计与说明不写在这里，见 [docs/](docs/README.md)。

## 1. 项目概览

AiAdmin 是基于 .NET 10 + Vue 3 的通用后台权限管理框架，前后端分离，后端三层单向引用：

| 目录                       | 职责                                                           | 依赖                                        |
|----------------------------|----------------------------------------------------------------|---------------------------------------------|
| `src/AiAdmin.Core`         | 实体模型、请求与结果契约、特性、雪花 ID 等共享类型             | 无                                          |
| `src/AiAdmin.Infrastructure` | 数据库上下文与种子、缓存、令牌、对象存储、动态查询等基础设施 | `AiAdmin.Core`                              |
| `src/AiAdmin.Api`          | 应用宿主、控制器、中间件、日志、计划作业、实时推送             | `AiAdmin.Core`、`AiAdmin.Infrastructure`    |
| `src/AiAdmin.Web`          | 前端（Vue 3 + Vite + Element Plus + Pinia + vue-i18n）         | 通过 HTTP 调用后端                          |
| `tests/AiAdmin.Tests`      | 后端 xUnit 测试                                                | 引用以上三个后端项目                        |
| `build`                    | 代码清理脚本、质量规则、版本标签脚本                           | -                                           |
| `docker`                   | 镜像构建、编排与滚动发布脚本                                   | -                                           |

各目录的详细说明见对应目录下的 `README.md`。

### 常用命令

| 目的               | 命令                                                                                                            |
|--------------------|-----------------------------------------------------------------------------------------------------------------|
| 后端运行           | `dotnet run --project src/AiAdmin.Api --launch-profile dev`                                                     |
| 前端安装与运行     | 在 `src/AiAdmin.Web` 下执行 `cnpm install`、`cnpm run dev -- --no-open`                                         |
| 后端编译           | `dotnet build`（要求 0 警告 0 错误）                                                                            |
| 后端测试           | `dotnet test tests/AiAdmin.Tests`                                                                               |
| 前端构建验证       | 在 `src/AiAdmin.Web` 下执行 `npx vite build`                                                                    |
| 前端工具函数测试   | 在 `src/AiAdmin.Web` 下对 `tests/*.test.ts` 使用 `node --import tsx --test`                                     |
| 代码清理（提交前） | `./build/code.clean.full.ps1`，包含 Prettier、ReSharper cleanup 与 `dot` 换行整理                               |
| 仅前端格式化       | `./build/code.clean.prettier.ps1`                                                                               |
| ReSharper 检查     | `jb inspectcode AiAdmin.slnx -f=Xml -e=HINT --no-build --dotnetcore="C:\Program Files\dotnet\dotnet.exe" --dotnetcoresdk=10.0.400` |

`AiAdmin.Api` 进程运行时会锁住 `dist/` 下的输出文件。无需停止进程，编译与测试时用 `-p:BaseOutputPath` 指定其他输出目录即可（目录放在 `dist/` 之外会被 git 识别为改动，可用 `dist/verify/bin/`）：

```
dotnet build -p:BaseOutputPath=D:/Work/SVN/Tao/AiAdmin/dist/verify/bin/
dotnet test tests/AiAdmin.Tests --no-build -p:BaseOutputPath=D:/Work/SVN/Tao/AiAdmin/dist/verify/bin/
```

不要同时覆盖 `BaseIntermediateOutputPath`，否则需要重新还原，测试项目会找不到 xunit。

## 2. 通用规范

- 每次 git commit 前、每次运行 `$git-log-and-commit` skill 前，必须运行 `./build/code.clean.full.ps1`
- 提交前除清理脚本外，还需 `dotnet build` 通过（0 警告 0 错误）并运行 `dotnet test tests/AiAdmin.Tests`
- 每次编译前端代码前必须运行 `./build/code.clean.prettier.ps1`
- 前端依赖安装、脚本执行统一使用 `cnpm`，不要使用 `pnpm`
- 所有新增或修改的用户界面文案必须接入 i18n，支持中英文切换
- 框架与业务内容分文件维护，框架文件会定期与上游合并，业务内容不得混入：界面文案放 `locales/business/{zh,en}.json`，服务端消息与接口描述的中文翻译放
  `utils/i18n/server-message.business.ts`，业务接口函数与类型放 `api/business-manage.ts`；框架文件（`locales/langs`、`server-message.ts`、`api/system-manage.ts`）
  只放框架内容，业务键与框架键不得重名。拿不准归属时，看这条消息或接口只被业务控制器使用还是也被用户、文件等框架控制器使用，后者留在框架文件
- 缓存只使用 Redis（`IDistributedCache` / `IConnectionMultiplexer`），禁止使用 `IMemoryCache`
- 正式版发布前（仓库没有 `v1` 及以上版本 tag）不考虑旧版本兼容、数据迁移或历史数据回填，按当前模型直接开发

## 3. 后端规范

### 3.1 接口与文档

- 每个 Controller 类必须标注 `ApiDescriptionAttribute`，每个公开接口方法也必须标注 `ApiDescriptionAttribute`，否则启动时的接口同步会抛出
  `Controller ... is missing ApiDescriptionAttribute` 或 `Action ... is missing ApiDescriptionAttribute`
- 每当新增 Controller 或 Action 并填写 `ApiDescriptionAttribute` 后，必须同步补全对应的中文翻译文件
- 系统所有 API 接口只允许使用 GET 或 POST 方法，不得使用 PUT、DELETE、PATCH 等其他 HTTP 方法
- 系统中所有对外发起的 HTTP 请求必须经过 `ExternalHttpRequestService` 或统一等价的请求封装，以记录请求地址、请求体、响应、状态码和耗时；禁止直接使用
  `HttpClient`、`IHttpClientFactory` 或其他 HTTP 调用方式绕过统一日志记录

### 3.2 代码风格与注释

- 新增或修改代码必须补充清晰的中文注释，说明文件职责以及关键类和方法的作用
- 每个类或 record 必须单独放在一个 `.cs` 文件中
- 类、方法和属性必须使用标准 .NET XML 文档注释，所有方法（包括 private 方法）均不得例外；方法参数必须使用 `<param>` 说明，返回值必须使用 `<returns>` 说明
- XML 文档注释的中文说明结尾不写句号
- XML 文档注释中，`<summary>` 的内容必须独占一行；`<param>`、`<returns>`、`<typeparam>` 等其他节点的内容与标签写在同一行
- `.cs` 文件中的字符串字面量不得包含中文，统一使用英文
- 成员顺序必须符合 StyleCop：静态成员在实例成员之前，`public` 在 `private` 之前。新增私有辅助方法放到同类私有方法所在区域，不得插在公开方法之间
- 返回 `Task` 或 `ValueTask` 的方法（包括私有方法和测试辅助方法）名称必须以 `Async` 结尾
- 业务代码按业务域归档：控制器、服务放在 `src/AiAdmin.Api/{业务}/{域}/`，请求、结果和实体放在 `src/AiAdmin.Core/{业务}/{域}/`，
  同一业务下多个域共用的类型放在 `{业务}/` 根目录；框架通用类型才放在 `Models`、`Contracts` 等公共目录，命名空间与目录保持一致

### 3.3 复杂度与可维护性

- 方法认知复杂度（Sonar `S3776`）以 Rider 阈值 10 为准，不得超过；超限时按职责拆分为独立私有方法（校验、字段应用、单项解析、重复分支改为数据驱动），
  不得通过关闭规则或放宽阈值规避。拆分后行为必须与原来一致，涉及接口行为时先确认已有测试覆盖

### 3.4 EF Core 与资源释放

- 参与 `.Contains` 等需要被 EF Core 翻译的内存集合使用 `List<T>`，不使用数组，避免 C# 14 重载决议变化导致的翻译差异
- `await using var` 声明的对象不得再手动 `DisposeAsync`；需要在断言前释放时使用 `await using (var x = ...) { }` 代码块
- `Task.Run` 的 lambda 不得直接捕获 `using var` 声明的变量，应通过带参数的辅助方法传入，避免“访问已释放的捕获变量”

### 3.5 静态检查与抑制策略

- 通过 `DispatchProxy` 等运行时生成的类型做的转换会被 ReSharper 误报为可疑类型转换，只在该行加 `// ReSharper disable once SuspiciousTypeConversion.Global`，
  不放宽整个项目的检查
- 请求 DTO 的集合属性由反序列化填充、响应 record 的位置参数由序列化读取，ReSharper 无法识别，已在 `AiAdmin.slnx.DotSettings` 关闭
  `CollectionNeverUpdated.Global`、`NotAccessedPositionalProperty.Global` 和 `UnusedAutoPropertyAccessor.Global`，不要为此把 `init` 改成 `set`

### 3.6 动态列表接口规范

- 所有表格列表查询统一使用 `POST {resource}/list`，请求体使用公共 `DynamicQueryRequest`，只包含 `current`、`size`、`dynamicFilter`、`sortField` 和
  `sortOrder`；分页结果统一返回 `PagedResponse<T>`
- 所有列表资源必须提供统一的 `GET {resource}/filter-fields` 元数据接口，返回 `ListFilterMetadataService.GetFields<TEntity>()`；实体的可筛选字段必须使用
  `ListFilterAttribute` 声明，`EntityBase.CreatedAt` 作为默认第一项日期范围字段
- 列表查询必须先对同一个 `IQueryable` 调用 `ApplyDynamicFilter`，再统计总数，然后调用 `ApplyDynamicSort`，最后执行分页；默认排序字段和方向必须显式指定
- `dynamicFilter` 必须支持 `And / Or` 嵌套组，并统一验证字段、操作符和值类型；无效字段、不支持的操作符或无法转换的值必须返回明确错误，禁止忽略条件后返回未筛选数据
- 动态筛选操作符的前后端定义必须一致。新增操作符时必须同时更新前端操作符列表、动态查询编辑器、后端 `DynamicFilterExtensions` 和对应测试
- 客户端列名、DTO 字段或格式化列与实体属性不一致时，后端必须通过排序/筛选别名映射到真实实体字段；导航集合、聚合字段等无法由通用表达式处理的场景允许集中扩展，但行为必须与公共动态查询协议一致
- Controller 不得自行解析零散筛选字段或复制动态查询表达式；通用行为必须沉淀到公共请求模型、元数据服务和查询扩展中，业务 Controller 只负责基础查询、必要的字段别名及结果投影
- 每个列表接口必须覆盖动态筛选与排序测试，至少包含字符串、数值、布尔、日期范围、集合、嵌套 And/Or、别名字段、非法操作符、非法排序字段和分页稳定性

### 3.7 测试

- 系统种子会内置多条计划作业和字典项，测试读取这类种子表时不得使用不带条件的 `SingleAsync()`，应按名称等业务键筛选，避免新增种子后测试失败

## 4. 前端规范

### 4.1 组件与交互

- 所有 `ElDrawer` 和 `ElDialog` 必须设置 `destroy-on-close`，关闭时销毁内容，避免状态和编辑器样式残留
- 单条删除操作统一使用气泡确认，批量删除操作统一使用弹窗确认
- `ArtTable` 操作栏按钮必须具备防抖机制；按钮触发 API 请求时，请求完成前必须禁用重复操作并将按钮图标替换为 loading 图标
- 所有表格包含 `IsEnabled` 字段时，必须使用 `Switch` 开关列展示和操作；`Switch` 开关列必须居中对齐
- `IsEnabled` 筛选列的栅格跨度统一为 `2`
- 表格中的数字字段列必须右对齐，ID 列为特例，统一左对齐
- 标签样式规范：除枚举类型或具有明确数量和语义的固定状态标签（如启用、禁用、是、否）外，数量不确定的动态业务标签统一使用基于显示文字 Hash
  的同色系配色；背景使用浅色，边框使用对应的更饱和色，文字使用正常正文色

### 4.2 弹窗与详情布局

- 详情弹窗包含结构化字段和原始数据时，页签名称统一使用“基本信息”和“原始数据”，不得使用含义模糊的“详情”作为基本信息页签名称
- 新增、编辑对话框使用多页签组织表单时，承载主要表单字段的首个页签统一命名为“基本信息”，不得使用“详情”等含义模糊的名称
- 所有详情抽屉和弹窗中的原始数据、独立预览页签必须撑满内容区剩余高度；从容器、页签到编辑器或 iframe 必须建立完整的可伸缩高度链路，禁止使用固定像素高度或
  `max-height` 导致大片留白。长内容在编辑器或预览内部滚动，页签栏和底部操作保持可见
- 原始数据统一复用 `ArtRawData` 和公共详情页签布局；公共组件必须让内部编辑器同步撑满外框。表单内嵌的小型编辑器按表单布局设置高度，不强制占满整个弹窗。
  必须验证页签切换、窗口缩放及关闭后重新打开时的高度

### 4.3 体积与清理

- 删除无用前端文件前，必须同时检查路由、后端菜单种子（`menu-seed.json` 与 `Data/*.cs` 的 `Component`）、`import.meta.glob`、`vite.config.ts` 的 `additionalData` 注入、
  `index.html` 引用和 `tests/` 目录，不能只看 `import` 图；清理后用 `vite build` 验证
- 全局注册的指令、插件会在启动时进入主包，新增前先确认确有页面使用；大型库（如 `highlight.js`）必须使用 `lib/core` 加按需语言的方式引入，
  避免拖大首屏包

## 5. 统一列表页规范（以用户管理为基准）

### 5.1 总则

- 用户管理页是系统表格列表页的标准模板；新增列表页或改造旧列表页时，页面结构、交互行为、查询协议和表格能力必须与用户管理保持一致
- 所有表格列表页统一使用 `ArtSearchBar + ArtTable + useTable` 方案，统一接入分页、刷新、列配置、服务端排序和 `cell-query`；不得直接使用 `ElTable` 另建一套列表行为
- 必须优先维护和复用统一列表页组件或组合式函数；页面层只允许配置资源标识、接口、列定义、默认条件和业务插槽，不得重复实现筛选拼装、保存查询、右键筛选、排序、分页等通用逻辑
- `ArtTablePage` 的 `filter-change` 事件必须绑定页面查询处理函数；处理函数必须通过 `useTable.replaceSearchParams({ dynamicFilter })` 写入唯一筛选状态并调用
  `getData`（或 `refreshData`），禁止遗漏事件绑定、解构不存在的 `useTable.handleFilterChange`，或只更新表单状态不发起查询

### 5.2 筛选

- 筛选栏第一项必须是日期时间范围选择器，默认筛选 `CreatedAt`，并提供 `CreatedAt / UpdatedAt` 切换；切换时必须保留已选择的时间范围。没有更新时间字段的资源可禁用切换，但日期范围仍必须位于第一项
- 基础筛选控件必须由后端实体属性上的 `ListFilterAttribute` 特性驱动，通过统一的 `filter-fields` 接口返回字段、i18n
  标签、控件类型、值类型、顺序、栅格宽度、占位符和选项；前端不得为同一字段重复硬编码一套筛选控件元数据
- 日期字段必须使用日期时间范围控件和 `DateRange` 操作符；范围语义统一为左闭右开。布尔、数值、枚举和字符串控件必须按后端元数据转换为正确的 JSON
  值类型，禁止把布尔值和数值无条件转换成字符串
- 基础筛选、高级查询、已保存查询、右键字段筛选和默认条件必须汇总到同一个 `dynamicFilter` JSON 树中，使用 `And / Or` 及嵌套 `filters`
  表达；禁止在请求中同时维护零散筛选参数和另一套动态筛选条件
- 动态查询 JSON 是筛选状态的唯一事实来源。控件修改时必须重建或更新该 JSON；应用已保存条件、右键条件或手工编辑 JSON
  后，必须同步更新可映射的基础控件和高级查询界面，条件数量、重置状态和实际请求必须保持一致
- 高级查询必须支持嵌套条件组、字段类型对应的操作符、JSON 预览/编辑，以及按当前路由保存、加载、删除自定义查询条件；保存和恢复时使用完整的 `dynamicFilter`，
  不得丢失条件分组或值类型
- 分组筛选栏的按钮样式必须统一展示数量徽标，分组项按数量倒序排列，数量相同时按显示名称升序稳定排序

### 5.3 表格列、右键与排序

- 可查询表格列必须声明准确的 `queryField`、`queryValueField`、`queryValueType` 和必要的 `queryOperators`；格式化列或一个单元格包含多个字段时，必须通过
  `data-query-*` 明确每个可操作字段。纯展示列和操作列必须设置 `queryField: false`
- 表格右键字段筛选必须根据字段类型展示并实际支持对应操作符：字符串包含/开头/结尾/等于/不等于/集合，数值和日期比较/范围/集合，布尔等于/不等于/集合；选择后必须合并进当前
  `dynamicFilter` 并立即查询，不得仅修改界面状态
- 表头排序和右键排序必须共用 `useTable` 的服务端排序状态，统一生成 `sortField` 与 `sortOrder`；前端列名与实体字段不一致时必须提供明确别名，禁止静默忽略无效排序字段
- 重置操作必须恢复页面声明的默认条件、清空高级及右键追加条件、清空排序并回到第一页；筛选、排序或已保存查询发生变化后也必须回到第一页

### 5.4 验证

- 新增或修改列表页时，至少验证日期类型切换、基础控件与 JSON 双向联动、高级查询保存/恢复、右键所有可见操作符、表头及右键排序、分页后条件保持和重置行为

## 6. 列表导出规范

- 列表页导出按钮统一使用带下载图标和下拉菜单的按钮样式，提供 Excel 和 JSON 两种导出格式
- 未勾选任何行时，导出当前查询条件和排序下的全部结果，不受当前分页页大小限制
- 勾选行后，仅导出勾选的数据
- 导出字段必须与列表展示字段保持一致，字段名称使用 i18n 文案；日期字段使用列表相同的格式化方式
- 导出过程必须具备 loading 或防重复操作机制，空结果需提示用户，不得生成空文件
- 未勾选导出时，前端必须调用后端专用 `export` 接口并由后端应用最大导出条数，禁止通过循环调用 `list` 接口模拟导出