# GoDo EditorPlugin 使用指南

## 定位

本插件是 GoDo 的项目安装助手、健康检查工具、资源清单与 UI 配置管理入口，以及编辑器扩展宿主。顶部 `GoDo Framework` 只有一个打开入口；统一窗口通过左侧导航管理项目配置（Runtime 与 C#）、DataTable、ResourceManifest、UiConfig 与编辑器扩展。DataTable 在项目配置下方使用独立条目；输入、相机和 ECS 等已安装扩展按清单动态显示在“编辑器扩展”分组下。DataTable 离线编译前端及编辑器入口详见 `Tools/DataTable/USAGE.md`。这些工具不创建业务场景或 UI，也不参与导出后的游戏运行。

EditorPlugin 只依赖 Godot Editor API，不依赖 Services、ErrorHub 或其他运行时模块；Runtime 不反向依赖插件。

安装助手使用 GDScript，因此复制框架后无需先创建 C# 解决方案或编译即可启用。运行时仍使用 C#；目标项目在运行或导出游戏前仍需具备可用的 C# 解决方案并完成编译。

## 打包与迁移边界

核心框架的分发单元是排除 `Integrations/` 的 `addons/godo_framework/` 目录。核心 ZIP 保留运行时、编辑器资源与 DataTable 编译前端，排除 Markdown 文档；GUIDE Input、Phantom Camera 与 Friflo ECS 适配以独立 ZIP 保留原路径，按需叠加到已安装的核心目录。Friflo ECS 包额外保留上游 MIT `LICENSE`，但不内置 NuGet 程序集；目标项目必须自行声明对应依赖。发布物不包含当前仓库的 Demo、测试脚本、`.godot/`、`bin/`、`obj/`、根目录 `.csproj`、解决方案文件或 `project.godot`。使用说明以 GitHub 仓库中的对应文档为准。

框架不接管目标项目配置：不会创建 `.csproj` 或解决方案，不修改输入映射、导出预设和业务场景，也不会在启用插件时自动写入 Autoload。用户可在“项目配置”的统一检查结果中确认补齐 GoDo 自有的可选集成条件编译与 Release Debugger 裁剪；SDK、目标框架、Android 配置、程序集名称、NuGet 版本和其他构建配置始终由目标项目维护。框架会自动注册游戏导出过滤器：Debug 与 Release 导出都排除 `Editor/` 和 `Tools/`；Release 额外排除 `Debugger/`，Debug 保留游戏内 Debugger。

### 首次迁移

1. 将完整的 `addons/godo_framework/` 复制到目标项目同一路径，不拆分复制内部模块。
2. 在 Godot 中启用 `GoDo Framework` 插件；此操作只注册编辑器工具。
3. 按检查窗口提示准备目标项目的 C# 解决方案并完成一次 Debug 编译。
4. 检查全部通过后，显式点击“安装 Runtime”。

不要手工复制本仓库的 `project.godot` 或 `.csproj` 到目标项目，也不要同时手工注册和通过插件安装 `GoDoRuntime`。

### 版本升级

升级前先备份或提交目标项目，并确认业务代码没有直接修改框架目录。关闭 Godot 后，以新版本完整替换 `addons/godo_framework/`，不要只覆盖同名文件，否则已移除的旧文件可能残留并参与编译；重新打开项目后完成 C# 编译，再运行插件健康检查和项目回归验证。

版本升级默认不删除现有 `GoDoRuntime` Autoload；只要名称和路径仍匹配，插件会将其识别为已正确安装。涉及 public API、资源路径或迁移步骤的版本，必须以对应版本说明为准，不由插件静默改写业务代码。

`plugin.cfg` 的 `version` 是独立的 GoDoFramework 版本；`min_godot_version` 是允许安装 Runtime 的最低 Godot 版本；`tested_godot_version` 是该框架版本完成回归的最高 Godot 版本。当前 Godot 低于最低版本或 major 不同时，Setup 报错并阻止安装；高于已验证版本但 major 相同时只警告，不阻止已有项目运行或安装。警告代表组合尚未验证，使用方应升级框架或完成项目回归，不应手工修改元数据掩盖状态。

### 彻底移除

先通过插件卸载精确匹配的 `GoDoRuntime` Autoload，再禁用插件并关闭 Godot，最后删除完整的 `addons/godo_framework/` 目录。插件不会删除框架文件、业务文件或目标项目构建配置；若业务代码仍引用 `GoDo.*`，必须由项目自行解除这些引用。

## 启用与检查

1. 在“项目设置 → 插件”中启用 `GoDo Framework`。
2. 打开顶部 `GoDo Framework → 打开 GoDo Framework...`，进入“项目配置”。
3. 查看 GoDoFramework 版本、Godot 兼容性、Runtime 场景、Autoload 和重复注册检查结果。

启用插件只增加工具菜单，不修改 Autoload。禁用插件只移除菜单和对话框，不卸载 Runtime。

统一窗口是可手动关闭的非模态根工具窗口。由它打开的管理窗口，以及管理窗口继续打开的文件选择、编辑、确认和文本提示窗口，都以当前焦点窗口作为 transient 父级并使用 exclusive 模态层级：根窗口始终位于 GoDo 窗口栈底部，同一时间只有最上层窗口接收输入；取消或关闭后焦点逐层返回，不会隐藏、关闭或压低下面的 GoDo 窗口。该层级同时适配编辑器的单窗口与多窗口模式；插件不启用全局置顶，因此切换到其他应用时不会强制覆盖其他应用窗口。

### 可选编辑器扩展

宿主只在插件进入树时扫描一次 `res://addons/`、`res://addons/godo_framework/Integrations/` 与 `res://addons/godo_framework/Tools/` 的一级子目录，并读取固定名称 `godo_editor_extension.cfg`；未安装可选集成时，缺失的 `Integrations/` 目录会被静默忽略。扫描不递归、不轮询，也不使用 `_Process()`。清单必须提供唯一 `id`、显示名、精确匹配的宿主 API 版本和位于同一包目录内的 GDScript。清单按 `menu_section`、`menu_order`、扩展 ID 分组和排序：`data_tables` 在统一窗口中作为项目配置下方的独立条目，其他扩展按显示名成为“编辑器扩展”子条目；未安装的可选扩展不会生成空导航项。GUIDE Input、Phantom Camera 与 Friflo ECS 的报告、提示和操作直接嵌入各自右侧页面，不再保留额外的“状态”条目或主设置弹窗；需要写入项目时仍显示最上层模态确认。单个扩展失败会在自己的条目中显示加载错误，不阻断核心入口或其他扩展。

扩展加载只允许注册宿主管理页或显式工具操作，不等于安装运行时依赖。插件启用、Autoload 等项目修改仍由对应扩展先只读检查、展示确认，再执行幂等修改。宿主退出时按相反顺序停用扩展并清理页面、信号和窗口。扩展宿主与控制器不进入游戏生命周期，Release 不产生每帧调用或托管分配。

Friflo ECS 扩展检查根目录 `.csproj` 与可选 `Directory.Packages.props`，识别直接和中央 NuGet 版本，并报告缺少引用、版本不符或无法确定的项目结构。只有根目录唯一普通项目缺少依赖且未使用中央包管理时，用户才能在精确预览后确认写入；扩展先创建非覆盖 `.godo-backup` 备份，写入后重新检查。多个项目、中央包管理、变量/条件版本和损坏 XML 保持只读，扩展也不执行 restore/build。

### C# 项目配置

“项目配置”统一报告中的“C# 项目配置”检查项只检查当前已安装框架模块需要的规则：GUIDE Input、Phantom Camera、Friflo ECS 的存在检测与 `Compile Remove`，以及 Release / ExportRelease 下的 Debugger 裁剪。普通单项目缺项时可经精确预览确认修复；写入前创建非覆盖 `.godo-backup`，写入后重新解析检查。工具不添加 NuGet 包，不复制 Demo/Verification 规则，也不改变 SDK、TargetFramework、Android 条件或用户已有的同名配置。多个 `.csproj`、中央包管理、非 Godot SDK、损坏 XML 或已有冲突规则保持只读。

Runtime 安装与 C# 规则修复是统一报告中的两项独立操作：综合提示会同时列出两边的待办，但 C# 规则需要人工维护时不会阻止已经通过 Runtime 安全检查的安装操作。SDK、TargetFramework、中央包管理或自定义规则仍由项目维护者决定，插件不会为了安装 Runtime 而改写这些配置。

检查窗口会显示“C# 环境”状态。根目录缺少或存在多个 `.csproj`、尚未生成编辑器 Debug 程序集，或框架源码比程序集更新时显示错误，不额外提供创建或编译按钮。

窗口上方突出显示当前框架状态，并按 Godot 版本、框架资源、C# 环境、框架唯一性、Autoload 的顺序展示检查结果；尚未正确安装时，唯一性显示为“待检查”，但仍会提前扫描其他名称指向 Runtime 的冲突，避免安装出双实例。下方独立提示框居中显示建议操作以及安装、卸载和刷新结果。

## 资源清单管理

编辑器顶部工具栏提供原生样式的 `GoDo Framework → 打开 GoDo Framework...` 单一入口。统一窗口的“资源 → 资源清单”页直接列出项目内现有清单，并提供刷新、创建、校验和管理入口。“创建清单...”可以创建一个空的 `.tres` / `.res` 格式 `ResourceManifest`；创建成功后列表立即刷新、选中新清单并打开管理窗口。保存窗口取消时也会重新扫描列表，以反映用户在文件窗口中执行的外部删除。创建失败通常代表目标项目尚未完成 C# 编译，或已启用的插件尚未重新加载，导致清单脚本无法被编辑器加载或实例化。

在资源清单管理窗口点击“添加资源”，选择器只浏览 `res://`，并优先显示场景、`.tres/.res`、贴图、音频、字体与 3D 场景资源。可一次多选资源；双击或点击“添加”都会进入写入预览。插件会显示尚未写入的条目，确认后才一次保存全部 `ResourceManifestEntry`；没有 UID 的资源会在预览中明确列出，确认后插件才生成 UID、更新 Godot 的 UID 记录并以 `uid://` 写入清单。取消时不写入清单，也不生成 UID。成功提示只列出每条写入的资源路径；`Id` 与 `Locator` 可在管理窗口查看。默认 `Id` 使用资源路径去掉 `res://` 与扩展名后的形式，例如 `res://Features/Shop/Icon.png` 会生成 `Features/Shop/Icon`，以避免同名资源冲突；可在管理窗口改为更稳定的业务语义 ID，例如 `ui/icon_close`。

在统一窗口的资源清单页选择清单并打开管理窗口后，可通过工具栏查看和定位当前清单，并按 `Id` 或资源路径筛选条目。刷新、创建和切换清单统一在主页列表完成，详情页不重复显示项目清单数量或创建、切换入口。管理窗口在 `Id`、`Locator` 与 `UID Status` 三列中显示全部映射；状态使用 `Using UID`、`Convertible`、`Missing UID`、`Invalid UID` 或 `Unresolved`，悬停可查看完整路径和实际保存的 `uid://`。单击条目只选中，双击可直接编辑；也可点击“编辑”修改 `Id` 或 Locator。对于已有 `res://` 定位，可点击“生成并使用 UID”，确认后插件会生成或复用资源 UID，并把该条目更新为 `uid://`。新增、编辑和删除都会复制 `Entries` 后保存，并从磁盘重新加载核对条目内容；保存结果不一致时会明确报错，不显示仅存在于内存的条目。点击“删除”并确认后仅移除该映射；不会删除该 Locator 指向的资源文件。校验与失败提示显示在当前窗口上层，关闭提示后会回到原管理窗口。

在统一窗口“资源 → 资源清单”页选中 `.tres` 或 `.res` 格式的 `ResourceManifest`，再点击“校验选中项”。校验器只读取资源并输出报告，不生成清单、不修复路径，也不写入任何项目文件。

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

- 当前引擎不低于 `plugin.cfg` 的 `min_godot_version`；兼容版本统一使用 `major.minor.patch`，当前最低版本为 `4.7.0`，最高已验证版本为 `4.7.2`；
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
- `python Verification/Package/verify_core_package.py --godot <GodotMonoConsole>`：在临时干净项目中只复制 `addons/godo_framework/`，启用 EditorPlugin 后验证缺失可选集成目录不会报错，并验证核心运行时不依赖可选适配包或第三方插件；烟雾场景同时编译并执行 StateMachine 的继承、Change、Tick 与 Dispose。
- `python Verification/Package/verify_core_package_lifecycle.py --godot <GodotMonoConsole>`：从真实核心 ZIP 创建临时项目，通过插件界面验证首次安装与幂等复查、9 项长期服务启动、整目录替换清除合成旧文件且保留 Autoload、错误路径拒绝卸载、精确卸载与插件禁用；删除框架后只验证已解除 `GoDo.*` 引用的中性宿主可编译运行。该夹具验证替换流程，不宣称覆盖任意历史版本兼容性。
- 已在当前项目验证：启用插件后检查结果健康；禁用插件后菜单消失且 Autoload 保持不变。
- 已在第二个小项目验证：未安装、安装、重复安装、名称冲突、重复路径和安全卸载。
- `EditorExtensionUiRegression.gd` 会在 Headless Editor 中验证 DataTable 独立导航、编辑器扩展动态子条目、三项扩展内容直接嵌入右侧且不再显示“状态”条目，并确认 GUIDE Input 与 Phantom Camera 报告非空、健康状态下修改按钮禁用，以及 Friflo ECS 能识别当前项目、按状态控制安装按钮并展示备份确认。
- DataTable 阶段 C.2 / C.3 使用独立实验 Probe 真实执行检查、全量生成与单表选择生成，不加入永久 `run_all.py`。

当前项目不自动执行安装或卸载测试，避免修改现有 `project.godot`；生命周期自动化只在系统临时项目中运行并清理其精确框架路径。
