# AiAdmin.Web

前端应用，基于 Vue 3 + Vite + TypeScript + Element Plus + Pinia + vue-i18n。界面框架派生自 Art Design Pro，所有列表页围绕统一的 `ArtSearchBar + ArtTable + useTable` 方案构建。

## 开发

```powershell
cnpm install
cnpm run dev -- --no-open       # http://localhost:5679
npx vite build                  # 构建验证（vue-tsc 类型检查见下方说明）
node --import tsx --test tests/*.test.ts   # 动态查询工具函数测试
```

- 依赖安装和脚本执行统一使用 `cnpm`，不要使用 `pnpm`
- 编译前先运行仓库根目录的 `./build/code.clean.prettier.ps1`
- 环境变量：`.env`（通用，含端口 `VITE_PORT=5679` 和权限模式）、`.env.development`（开发，通过 `VITE_API_PROXY_URL` 代理到后端 `http://localhost:5678`）、
  `.env.production`（生产，`VITE_API_URL=/` 由网关转发）
- 路径别名：`@` 指向 `src`，另有 `@views`、`@imgs`、`@icons`、`@utils`、`@stores`、`@styles`

## 目录

```
src
├─ api/            接口函数与类型：system-manage.ts（框架）、business-manage.ts（业务）、auth、dashboard、files
├─ assets/         样式（styles）、图片、图标
├─ components/
│  ├─ core/        框架通用组件：base、charts、forms、layouts、others、tables、theme、views、widget
│  └─ business/    业务组件
├─ config/         全局配置：主题、布局、默认设置
├─ directives/     自定义指令：v-auth、v-roles、v-ripple
├─ enums/          枚举
├─ hooks/core/     组合式函数：useTable、useAuth、useTheme、useCommon 等
├─ locales/        langs（框架语言包）、business（业务语言包）
├─ router/         core（路由注册、校验、组件加载）、guards（守卫）、modules（静态路由）、routes
├─ store/modules/  Pinia：user、menu、setting、worktab、table
├─ types/          类型声明，其中 types/import 为自动导入生成文件
├─ utils/          http、i18n、table、json（动态查询）、realtime、socket、storage 等
└─ views/          页面：system 为框架页面，business 为业务页面，其余为 auth、dashboard、finance 等
```

## 核心约定

### 统一列表页

用户管理（`views/system/user`）是标准模板。列表页只配置资源标识、接口、列定义、默认条件和业务插槽，筛选、保存查询、右键筛选、排序、分页等通用能力全部由公共组件提供：

- `ArtTablePage`（`components/core/tables/art-table-page`）：组合筛选栏、表格和高级查询，`filter-change` 事件必须绑定页面的查询处理函数
- `ArtSearchBar`：筛选栏，基础控件由后端 `filter-fields` 接口驱动
- `ArtDynamicQueryDrawer`：高级查询，支持嵌套条件组、JSON 预览编辑、保存与加载
- `ArtTable` / `useTable`：分页、刷新、列配置、服务端排序和 `cell-query` 右键筛选；`replaceSearchParams({ dynamicFilter })` 是写入筛选状态的唯一入口
- 所有筛选条件汇总为一棵 `dynamicFilter` JSON 树，是筛选状态的唯一事实来源，工具函数见 `utils/json/dynamic-query*.ts`，对应测试在 `tests/`

可查询的列必须声明 `queryField`、`queryValueField`、`queryValueType`；纯展示列和操作列设置 `queryField: false`。完整规则见 [AGENTS.md](../../AGENTS.md) 第 5 节。

### 列表导出

导出按钮提供 Excel 和 JSON 两种格式，未勾选行时调用后端 `export` 接口导出当前查询条件下的全部结果，勾选行时只导出所选数据，导出字段与列表保持一致。规则见 AGENTS.md 第 6 节。

### 国际化

- 所有界面文案接入 i18n（中文、英文），不得在页面中写死文案
- 框架文案放 `locales/langs/{zh,en}.json`，业务文案放 `locales/business/{zh,en}.json`，加载时深度合并，键名不得重名
- 服务端消息与接口描述的中文翻译：框架放 `utils/i18n/server-message.ts`，业务放 `server-message.business.ts`
- 框架文件会定期与上游合并，业务内容不要写进框架文件

### 权限与菜单

- 菜单由后端菜单种子（`Infrastructure/Data/menu-seed.json`）下发，`component` 字段对应 `views` 下的目录，页面由 `import.meta.glob` 动态加载
- 权限模式由 `VITE_ACCESS_MODE` 决定：`frontend` 模式使用前端路由元数据，`backend` 模式使用后端下发的菜单与按钮权限
- 按钮权限使用 `v-auth` 指令或 `useAuth().hasAuth('标识')`，标识来自菜单元数据的 `authList`
- 新增页面需同步：`views` 页面、后端菜单种子、路由权限（`roles`）、i18n 标题键

### 请求与实时推送

- 请求封装在 `utils/http`：自动携带 `Authorization` 与 `Accept-Language`，统一处理错误和按钮 loading，接口函数放 `api/`
- 实时推送通过 WebSocket（`utils/socket`、`utils/realtime`）接收版本升级和站内信未读数，令牌通过查询参数传递

## 体积与清理

- 页面和较重的库按需加载；全局注册的指令、插件会进入主包，新增前确认确有页面使用
- 大型库（如 `highlight.js`）使用 `lib/core` 加按需语言引入，避免拖大首屏
- 删除无用文件前必须同时检查路由、后端菜单种子、`import.meta.glob`、`vite.config.ts` 的 `additionalData` 注入、`index.html` 引用和 `tests/` 目录，不能只看 `import` 图，清理后用 `vite build` 验证

## 已知问题

当前环境中 `vue-tsc` 与 `eslint` 无法运行（`vue-tsc` 报 `typescript/lib/tsc` 未导出，`eslint` 找不到 `vue-eslint-parser`），因此类型检查与 lint 暂不可用，请以 `vite build` 作为构建验证，并在 Rider 或 IDE 中查看类型提示。修复依赖问题后请更新本节。