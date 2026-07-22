# Services 使用指南

## 定位与优势

Services 是面向业务层的最小长期服务注册表。它让业务代码依赖 `ISceneService`、`IAudioService` 等接口，而不是硬编码 Autoload 节点路径或持有具体实现。

它不是依赖注入容器：不自动构造对象、不解析依赖、不管理任意对象生命周期，也不允许 Core 模块借它横向查找彼此。

## 业务层获取服务

```csharp
ISceneService scenes = Services.Get<ISceneService>();
await scenes.ChangeAsync(levelKey);
```

确定服务可能未启用时使用 `TryGet`：

```csharp
if (Services.TryGet<ISceneService>(out ISceneService? scenes))
    GD.Print($"Scene service ready: {!scenes.IsChanging}");
```

`Get<T>()` 缺失时抛出 `InvalidOperationException`；`TryGet<T>()` 缺失时返回 false 并输出 null。

## 注册与注销

注册通常由 GoDoRuntime 在框架启动阶段完成，业务代码一般只查询：

```csharp
Services.Register<ISceneService>(sceneService);

// 退出时必须传入同一个实例。
Services.Unregister<ISceneService>(sceneService);
```

- 只能按接口注册，具体类型注册会失败。
- 同一接口不能重复注册；首版不提供隐式 Replace。
- `Unregister` 只有在实例引用与当前注册值完全相同时才成功。
- GoDoRuntime 退出时统一清空注册表。

## 生命周期与线程

- 所有 API 只能在 Godot 主线程调用。
- 注册对象必须比所有使用者活得更久；Services 只保存引用，不替对象 Dispose。
- 短生命周期对象、关卡节点或临时数据不应注册为全局服务。
- 测试替换服务时应先注销原实例，并在测试结束后恢复，避免污染其他用例。

## 自动回归验证

`Verification/Automated/ServicesRegression.tscn` 使用专属测试接口验证缺失查询、注册与获取、重复注册、具体类型拒绝、实例匹配注销和注销后状态，不会清空或替换 GoDoRuntime 已注册的真实服务。

```powershell
& $env:GODOT_PATH --headless --path . Verification/Automated/ServicesRegression.tscn
```

当前 runner 已通过 `dotnet build` 编译，并在项目声明的 Godot Mono Headless 版本中完成 6/6 项验证；成功退出码为 0，失败退出码为 1。

## 常见误用

| 应该 | 避免 |
|---|---|
| 注册职责清晰的长期接口 | 注册任意具体类当全局变量 |
| 业务层通过 Services 访问框架服务 | 框架内部用 Services 隐藏依赖 |
| 缺失服务明确失败或使用 TryGet | 捕获所有异常后静默忽略 |
| 生命周期入口注册和注销 | 每帧 Get 后长期缓存失效服务 |
