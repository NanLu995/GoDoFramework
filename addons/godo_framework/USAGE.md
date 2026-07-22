# GoDo EditorPlugin 使用指南

## 定位

本插件是 GoDo 的项目安装助手、健康检查工具、资源清单校验入口与编辑器扩展宿主。它负责检查、安装和卸载 `GoDoRuntime` Autoload，只读校验 `ResourceManifest`，并通过 `godo_editor_extension.cfg` 注册可选集成与框架 Tools 的顶部 `GoDo Framework` 菜单项。DataTable 离线编译前端及编辑器入口详见 `Tools/DataTable/USAGE.md`。这些工具不创建业务场景或 UI，不修改 C# 项目文件，也不参与导出后的游戏运行。

EditorPlugin 只依赖 Godot Editor API，不依赖 Services、ErrorHub 或其他运行时模块；Runtime 不反向依赖插件。

安装助手使用 GDScript，因此复制框架后无需先创建 C# 解决方案或编译即可启用。运行时仍使用 C#；目标项目在运行或导出游戏前仍需具备可用的 C# 解决方案并完成编译。

## 打包与迁移边界

框架的唯一分发单元是 `addons/godo_framework/` 目录。发布 ZIP 保留其中的运行与编辑器资源，排除 Markdown 文档；也不包含当前仓库的 Demo、测试脚本、`.godot/`、`bin/`、`obj/`、根目录 `.csproj`、解决方案文件或 `project.godot`。使用说明以 GitHub 仓库中的对应文档为准。

框架不接管目标项目配置：不会创建或修改 `.csproj`、解决方案、输入映射、导出预设和业务场景，也不会在启用插件时自动写入 Autoload。目标项目仍负责自身的 Godot/.NET 版本、程序集名称、构建和导出配置。

### 首次迁移

1. 将完整的 `addons/godo_framework/` 复制到目标项目同一路径，不拆分复制内部模块。
2. 在 Godot 中启用 `GoDo Framework` 插件；此操作只注册编辑器工具。
3. 按检查窗口提示准备目标项目的 C# 解决方案并完成一次 Debug 编译。
4. 检查全部通过后，显式点击“安装 Runtime”。

不要手工复制本仓库的 `project.godot` 或 `.csproj` 到目标项目，也不要同时手工注册和通过插件安装 `GoDoRuntime`。

### 版本升级

升级前先备份或提交目标项目，并确认业务代码没有直接修改框架目录。关闭 Godot 后，以新版本完整替换 `addons/godo_framework/`，不要只覆盖同名文件，否则已移除的旧文件可能残留并参与编译；重新打开项目后完成 C# 编译，再运行插件健康检查和项目回归验证。

版本升级默认不删除现有 `GoDoRuntime` Autoload；只要名称和路径仍匹配，插件会将其识别为已正确安装。涉及 public API、资源路径或迁移步骤的版本，必须以对应版本说明为准，不由插件静默改写业务代码。

### 彻底移除

先通过插件卸载精确匹配的 `GoDoRuntime` Autoload，再禁用插件并关闭 Godot，最后删除完整的 `addons/godo_framework/` 目录。插件不会删除框架文件、业务文件或目标项目构建配置；若业务代码仍引用 `GoDo.*`，必须由项目自行解除这些引用。

## 启用与检查

1. 在“项目设置 → 插件”中启用 `GoDo Framework`。
2. 打开顶部 `GoDo Framework → 配置 (Setup)...` 窗口。
3. 查看 Godot 版本、Runtime 场景、Autoload 和重复注册检查结果。

启用插件只增加工具菜单，不修改 Autoload。禁用插件只移除菜单和对话框，不卸载 Runtime。

### 可选编辑器扩展

宿主只在插件进入树时扫描一次 `res://addons/`、`res://addons/godo_framework/Integrations/` 与 `res://addons/godo_framework/Tools/` 的一级子目录，并读取固定名称 `godo_editor_extension.cfg`；不递归扫描、不轮询，也不使用 `_Process()`。清单必须提供唯一 `id`、显示名、精确匹配的宿主 API 版本和位于同一包目录内的 GDScript。清单按 `menu_section`、`menu_order`、扩展 ID 分组和排序：`data_tables` 归入“数据表”，其他扩展归入“编辑器扩展”。单个扩展失败只记录到“编辑器扩展状态...”，不阻断核心菜单或其他扩展。

扩展加载只允许注册菜单和延迟创建编辑器窗口，不等于安装运行时依赖。插件启用、Autoload 等项目修改仍由对应扩展先只读检查、展示确认，再执行幂等修改。宿主退出时按相反顺序停用扩展并清理菜单、信号和窗口。扩展宿主与控制器不进入游戏生命周期，Release 不产生每帧调用或托管分配。

检查窗口会显示“C# 环境”状态。根目录缺少或存在多个 `.csproj`、尚未生成编辑器 Debug 程序集，或框架源码比程序集更新时显示错误，不额外提供创建或编译按钮。

窗口上方突出显示当前框架状态，并按 Godot 版本、框架资源、C# 环境、框架唯一性、Autoload 的顺序展示检查结果；尚未正确安装时，唯一性显示为“待检查”，但仍会提前扫描其他名称指向 Runtime 的冲突，避免安装出双实例。下方独立提示框居中显示建议操作以及安装、卸载和刷新结果。

## 资源清单管理

编辑器顶部工具栏提供原生样式的 `GoDo Framework` 下拉入口。菜单按“配置”“资源管理”“数据表”“编辑器扩展”分组；资源管理中先列出创建、管理和校验清单，再以分隔线区分“选择资源并添加”。其中的“创建资源清单 (Create Resource Manifest)...”可以创建一个空的 `.tres` / `.res` 格式 `ResourceManifest`。创建失败通常代表目标项目尚未完成 C# 编译，或已启用的插件尚未重新加载，导致清单脚本无法被编辑器加载或实例化。

打开顶部工具栏 `GoDo Framework` 中的“选择资源并添加 (Select Resource to Add)...”，选择器只浏览 `res://`，并优先显示场景、`.tres/.res`、贴图、音频、字体与 3D 场景资源。可一次多选资源；双击或点击“添加”都会进入目标清单选择。项目内恰好一份 `ResourceManifest` 时自动作为目标；多份时才弹出选择器并显示“选择目标资源清单（发现 N 份）”；没有时拒绝添加并提示先创建。确定目标后，插件会显示“尚未写入”的预览，确认添加才会一次保存全部 `ResourceManifestEntry`；没有 UID 的资源会在预览中明确列出，确认后插件才生成 UID、更新 Godot 的 UID 记录并以 `uid://` 写入清单。取消时不写入清单，也不生成 UID。成功提示只列出每条写入的资源路径；`Id` 与 `Locator` 可在管理窗口查看。默认 `Id` 使用资源路径去掉 `res://` 与扩展名后的形式，例如 `res://Features/Shop/Icon.png` 会生成 `Features/Shop/Icon`，以避免同名资源冲突；可在 Inspector 中改为更稳定的业务语义 ID，例如 `ui/icon_close`。

打开“管理资源清单 (Manage Resource Manifest)...”后，项目内恰好一份清单时会直接打开；多份时才要求选择目标。管理窗口在 `Id`、“定位”与“UID 状态”三列中显示全部映射；定位始终优先显示可读的 `res://` 路径，UID 状态直接标示“已使用 UID”“可转换为 UID”“缺少 UID”或“UID 无效”，悬停可查看完整路径和实际保存的 `uid://`。单击条目只选中，双击可直接编辑；也可点击“编辑选中项”修改 `Id` 或 Locator。对于已有 `res://` 定位，可点击“生成并使用 UID”，确认后插件会生成或复用资源 UID，并把该条目更新为 `uid://`。工具会校验空值、重复 ID、Locator 前缀与资源是否存在，再通过 `ResourceSaver` 保存。点击“删除选中项”并确认后仅移除该映射；不会删除该 Locator 指向的资源文件。

打开编辑器顶部“GoDo Framework → 资源管理 → 校验资源清单 (Validate Resource Manifest)...”，选择 `.tres` 或 `.res` 格式的 `ResourceManifest` 资源。校验器只读取资源并输出报告，不生成清单、不修复路径，也不写入任何项目文件。

当前检查内容包括：

- 资源是否能被 `ResourceLoader` 加载；
- 是否包含 `Entries` 数组；
- 每个条目是否存在非空 `Id` 与 `Locator`；
- `Id` 是否重复；
- `Locator` 是否以 `res://` 或 `uid://` 开头；
- `Locator` 当前是否能被 `ResourceLoader` 解析。

无法解析的 Locator 会作为警告展示，便于迁移过程中先发现缺失导入、UID 失效或路径移动问题；空 ID、重复 ID 和非法 Locator 前缀作为错误展示。该工具不替代运行时 `ResourceRegistry.Load` 的合并规则，运行时仍以已加载 Manifest 的实际内容为准。

## 安装

只有同时满足以下条件时“安装 Runtime”按钮才可用：

- 当前引擎为 Godot 4.7 或更高的 4.x 版本；
- 根目录存在 `.csproj`，并且已经至少成功编译一次；
- `res://addons/godo_framework/Core/GoDoRuntime.tscn` 存在；最终场景类型由 Godot 的 Autoload 安装 API 校验；
- `GoDoRuntime` 名称尚未被占用；
- 没有其他 Autoload 名称指向同一个 Runtime 场景。

安装按钮会再次执行全部检查；C# 环境未就绪或 `project.godot` 无法读取时拒绝安装，避免添加无法实例化的 Autoload。安装使用 Godot EditorPlugin 的 Autoload API，并在调用后重新检查实际状态；已正确安装时不会重复写入，名称冲突和重复注册只报告，不自动覆盖或删除。

## 卸载

卸载前必须经过确认，确认时会重新检查项目配置和 Autoload 状态。插件只会移除名称为 `GoDoRuntime` 且精确指向框架 Runtime 场景的 Autoload；路径不匹配或 `project.godot` 无法读取时拒绝操作。

卸载不会删除框架文件、业务文件或其他 Autoload。禁用插件也不会触发卸载。

## 失败语义

- 健康检查和资源清单校验是只读操作，问题以正常、警告、错误检查项展示。
- 创建资源清单只写入用户通过保存对话框选择的目标文件；添加选中资源会在确认后写入用户选择的目标 `ResourceManifest`，并可能为缺少 UID 的源资源更新 Godot UID 记录。
- 添加资源仅接受 `res://` 内、可由 `ResourceLoader` 加载的非脚本资源；`ResourceManifest`、自引用、文件夹与无效资源会在写入前拒绝。
- 添加资源发现重复 `Id` 时拒绝写入，避免静默覆盖已有映射。
- 删除条目只移除目标 `ResourceManifest` 中的一条映射；必须经过确认，且不会删除任何资源文件。
- 添加时项目内恰好一份 `ResourceManifest` 会自动选中它；多份清单由用户显式选择，运行时则应使用 `ResourceRegistry.LoadMerge` 明确加载顺序。
- 自动查找清单只扫描项目源目录，跳过 `.godot` 等生成目录，避免读取旧导出或导入缓存。
- 预期冲突不会抛出到编辑器，而是禁用安装或拒绝卸载并显示原因。
- 残缺安装仍允许卸载精确匹配的 Autoload；C# 环境未就绪不会阻止该清理操作。
- 其他名称指向 Runtime 场景时只报告重复项，不擅自删除无法确认归属的项目配置。
- Editor API 调用后以复查结果判断是否成功；失败时保留检查结果并提示查看编辑器输出。
- 所有写操作完成后重新检查，不能仅凭 Editor API 调用返回判断成功。

## 验证

- 已在未创建 C# 解决方案、未编译的新建 .NET 项目中验证：复制框架后可直接启用插件并打开检查窗口。
- `dotnet build GoDoFramework.sln`：验证运行时代码可编译。
- `python Verification/Package/verify_core_package.py --godot <GodotMonoConsole>`：在临时干净项目中只复制 `addons/godo_framework/`，验证核心包不依赖可选适配包或第三方插件。
- 已在当前项目验证：启用插件后检查结果健康；禁用插件后菜单消失且 Autoload 保持不变。
- 已在第二个小项目验证：未安装、安装、重复安装、名称冲突、重复路径和安全卸载。
- `EditorExtensionUiRegression.gd` 会在 Headless Editor 中验证菜单分组、顺序和资源添加项前的分隔线，并真实触发已安装扩展的菜单，确认 GUIDE Input 与 Phantom Camera 报告非空、健康状态下修改按钮禁用。
- DataTable 阶段 C.2 / C.3 使用独立实验 Probe 真实执行检查、全量生成与单表选择生成，不加入永久 `run_all.py`。

当前项目不自动执行安装或卸载测试，避免修改现有 `project.godot`。
