#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

#nullable enable

namespace GoDo;

public sealed partial class DebuggerOverlay : CanvasLayer
{

    private void CacheSystemNodes()
    {
        _systemPlatformValue = GetSystemNode<Label>("Summary/PlatformCard/Content/Value");
        _systemPlatformDetail = GetSystemNode<Label>("Summary/PlatformCard/Content/Detail");
        _systemBuildValue = GetSystemNode<Label>("Summary/BuildCard/Content/Value");
        _systemBuildDetail = GetSystemNode<Label>("Summary/BuildCard/Content/Detail");
        _systemRendererValue = GetSystemNode<Label>("Summary/RendererCard/Content/Value");
        _systemRendererDetail = GetSystemNode<Label>("Summary/RendererCard/Content/Detail");
        _systemUptimeValue = GetSystemNode<Label>("Summary/UptimeCard/Content/Value");
        _systemDetailsTree = GetSystemNode<Tree>("Details");

        _systemDetailsTree.SetColumnTitle(0, "分类");
        _systemDetailsTree.SetColumnTitle(1, "项目");
        _systemDetailsTree.SetColumnTitle(2, "值");
        _systemDetailsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _systemDetailsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _systemDetailsTree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _systemDetailsTree.SetColumnExpand(0, false);
        _systemDetailsTree.SetColumnExpand(1, false);
        _systemDetailsTree.SetColumnExpand(2, true);
        _systemDetailsTree.SetColumnCustomMinimumWidth(0, 72);
        _systemDetailsTree.SetColumnCustomMinimumWidth(1, 132);
        BuildSystemRows();
        InitializeSystemStaticValues();
    }

    private void BuildSystemRows()
    {
        _systemDetailsTree!.Clear();
        TreeItem root = _systemDetailsTree.CreateItem();
        TreeItem runtime = CreateSystemGroup(root, "运行时");
        _systemMetricRows[SystemMetricGodotVersion] = CreateSystemRow(runtime, "Godot");
        _systemMetricRows[SystemMetricDotNetRuntime] = CreateSystemRow(runtime, ".NET");
        _systemMetricRows[SystemMetricBuild] = CreateSystemRow(runtime, "构建");
        _systemMetricRows[SystemMetricProcessId] = CreateSystemRow(runtime, "Process ID");
        _systemMetricRows[SystemMetricProcessArchitecture] = CreateSystemRow(runtime, "进程架构");

        TreeItem platform = CreateSystemGroup(root, "平台");
        _systemMetricRows[SystemMetricPlatform] = CreateSystemRow(platform, "操作系统");
        _systemMetricRows[SystemMetricOsVersion] = CreateSystemRow(platform, "系统版本");
        _systemMetricRows[SystemMetricLocale] = CreateSystemRow(platform, "Locale");

        TreeItem window = CreateSystemGroup(root, "窗口");
        _systemMetricRows[SystemMetricDisplayServer] = CreateSystemRow(window, "Display Server");
        _systemMetricRows[SystemMetricWindowMode] = CreateSystemRow(window, "窗口模式");
        _systemMetricRows[SystemMetricWindowSize] = CreateSystemRow(window, "窗口尺寸");
        _systemMetricRows[SystemMetricScreenSize] = CreateSystemRow(window, "屏幕尺寸");
        _systemMetricRows[SystemMetricVsync] = CreateSystemRow(window, "VSync");

        TreeItem rendering = CreateSystemGroup(root, "渲染");
        _systemMetricRows[SystemMetricRenderingMethod] = CreateSystemRow(rendering, "Method");
        _systemMetricRows[SystemMetricRenderingDriver] = CreateSystemRow(rendering, "Driver");
        _systemMetricRows[SystemMetricAdapter] = CreateSystemRow(rendering, "显卡");
        _systemMetricRows[SystemMetricAdapterVendor] = CreateSystemRow(rendering, "厂商");
        _systemMetricRows[SystemMetricAdapterType] = CreateSystemRow(rendering, "类型");
    }

    private void InitializeSystemStaticValues()
    {
        string platform = ReadSystemValue(OS.GetName);
        string osVersion = ReadSystemValue(OS.GetVersion);
        string godotVersion = ReadSystemValue(
            () => Engine.GetVersionInfo()["string"].AsString());
        string dotNetRuntime = ReadSystemValue(() => RuntimeInformation.FrameworkDescription);
        string processArchitecture = ReadSystemValue(
            () => RuntimeInformation.ProcessArchitecture.ToString());
        string processId = ReadSystemValue(
            () => OS.GetProcessId().ToString(CultureInfo.InvariantCulture));
        string displayServer = ReadSystemValue(DisplayServer.GetName);
        string renderingMethod = ReadSystemValue(
            () => FormatRenderingMethod(RenderingServer.GetCurrentRenderingMethod()));
        string renderingDriver = ReadSystemValue(
            () => FormatRenderingDriver(RenderingServer.GetCurrentRenderingDriverName()));
        string adapter = ReadSystemValue(RenderingServer.GetVideoAdapterName);
        string adapterVendor = ReadSystemValue(RenderingServer.GetVideoAdapterVendor);
        string adapterType = ReadSystemValue(
            () => FormatVideoAdapterType(RenderingServer.GetVideoAdapterType()));

        _systemPlatformValue!.Text = platform;
        _systemPlatformValue.TooltipText = platform;
        _systemPlatformDetail!.Text = osVersion;
        _systemPlatformDetail.TooltipText = osVersion;
        _systemBuildValue!.Text = "Debug";
        _systemBuildDetail!.Text = $"Godot {godotVersion} / .NET";
        _systemBuildDetail.TooltipText = $"{godotVersion} / {dotNetRuntime}";
        _systemRendererValue!.Text = renderingMethod;
        _systemRendererValue.TooltipText = renderingMethod;
        _systemRendererDetail!.Text = renderingDriver;
        _systemRendererDetail.TooltipText = renderingDriver;

        SetSystemValue(SystemMetricGodotVersion, godotVersion);
        SetSystemValue(SystemMetricDotNetRuntime, dotNetRuntime);
        SetSystemValue(SystemMetricBuild, "Debug");
        SetSystemValue(SystemMetricProcessId, processId);
        SetSystemValue(SystemMetricProcessArchitecture, processArchitecture);
        SetSystemValue(SystemMetricPlatform, platform);
        SetSystemValue(SystemMetricOsVersion, osVersion);
        SetSystemValue(SystemMetricDisplayServer, displayServer);
        SetSystemValue(SystemMetricRenderingMethod, renderingMethod);
        SetSystemValue(SystemMetricRenderingDriver, renderingDriver);
        SetSystemValue(SystemMetricAdapter, adapter);
        SetSystemValue(SystemMetricAdapterVendor, adapterVendor);
        SetSystemValue(SystemMetricAdapterType, adapterType);
    }

    private static string FormatRenderingMethod(string method)
    {
        return method switch
        {
            "forward_plus" => "Forward+",
            "mobile" => "Mobile",
            "gl_compatibility" => "Compatibility",
            _ => method,
        };
    }

    private static string FormatRenderingDriver(string driver)
    {
        return driver switch
        {
            "vulkan" => "Vulkan",
            "d3d12" => "Direct3D 12",
            "metal" => "Metal",
            "opengl3" => "OpenGL 3",
            "opengl3_es" => "OpenGL ES 3",
            "opengl3_angle" => "OpenGL 3 (ANGLE)",
            _ => driver,
        };
    }

    private static string FormatVideoAdapterType(RenderingDevice.DeviceType type)
    {
        return type switch
        {
            RenderingDevice.DeviceType.IntegratedGpu => "集成显卡",
            RenderingDevice.DeviceType.DiscreteGpu => "独立显卡",
            RenderingDevice.DeviceType.VirtualGpu => "虚拟显卡",
            RenderingDevice.DeviceType.Cpu => "软件渲染",
            RenderingDevice.DeviceType.Other => "其他 / 未知",
            _ => type.ToString(),
        };
    }

    private static TreeItem CreateSystemGroup(TreeItem root, string title)
    {
        TreeItem group = root.CreateChild();
        group.SetText(0, title);
        group.SetSelectable(0, false);
        group.Collapsed = false;
        return group;
    }

    private static TreeItem CreateSystemRow(TreeItem group, string property)
    {
        TreeItem row = group.CreateChild();
        row.SetText(1, property);
        row.SetText(2, "不可用");
        row.SetTextAlignment(1, HorizontalAlignment.Left);
        row.SetTextAlignment(2, HorizontalAlignment.Left);
        return row;
    }

    private void SetSystemValue(int index, string value)
    {
        TreeItem? row = _systemMetricRows[index];
        if (row is null)
            return;

        string displayValue = string.IsNullOrWhiteSpace(value) ? "不可用" : value;
        row.SetText(2, displayValue);
        row.SetTooltipText(2, displayValue);
    }

    private T GetSystemNode<T>(string path) where T : Node
    {
        T? node = _systemDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerSystem 场景缺少节点：{path}");
    }

    private static string ReadSystemValue(Func<string> read)
    {
        try
        {
            string value = read();
            return string.IsNullOrWhiteSpace(value) ? "不可用" : value;
        }
        catch
        {
            return "不可用";
        }
    }

    private void CachePerformanceNodes()
    {
        _performanceFpsValue =
            GetPerformanceNode<Label>("Content/Summary/FpsCard/Content/Value");
        _performanceProcessValue =
            GetPerformanceNode<Label>("Content/Summary/ProcessCard/Content/Value");
        _performancePhysicsValue =
            GetPerformanceNode<Label>("Content/Summary/PhysicsCard/Content/Value");
        _performanceMemoryValue =
            GetPerformanceNode<Label>("Content/Summary/MemoryCard/Content/Value");
        _performanceManagedMemoryValue =
            GetPerformanceNode<Label>("Content/Summary/ManagedMemoryCard/Content/Value");
        _performanceFrameGraph =
            GetPerformanceNode<Control>("Content/Trends/FramePanel/Content/Graph");
        _performanceMemoryGraph =
            GetPerformanceNode<Control>("Content/Trends/MemoryPanel/Content/Graph");
        _performanceMetricsTree = GetPerformanceNode<Tree>("Content/Metrics");

        _performanceMetricsTree.SetColumnTitle(0, "分类");
        _performanceMetricsTree.SetColumnTitle(1, "指标");
        _performanceMetricsTree.SetColumnTitle(2, "数值");
        _performanceMetricsTree.SetColumnTitle(3, "说明");
        _performanceMetricsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _performanceMetricsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _performanceMetricsTree.SetColumnTitleAlignment(2, HorizontalAlignment.Right);
        _performanceMetricsTree.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        _performanceMetricsTree.SetColumnExpand(0, false);
        _performanceMetricsTree.SetColumnExpand(1, true);
        _performanceMetricsTree.SetColumnExpand(2, false);
        _performanceMetricsTree.SetColumnExpand(3, true);
        _performanceMetricsTree.SetColumnCustomMinimumWidth(0, 62);
        _performanceMetricsTree.SetColumnCustomMinimumWidth(2, 108);
        BuildPerformanceMetricRows();
    }

    private void BuildPerformanceMetricRows()
    {
        _performanceMetricsTree!.Clear();
        TreeItem root = _performanceMetricsTree.CreateItem();
        TreeItem memory = CreatePerformanceMetricGroup(root, "内存");
        _performanceMetricRows[PerformanceMetricEngineMemory] =
            CreatePerformanceMetricRow(memory, "引擎当前", "字节");
        _performanceMetricRows[PerformanceMetricEngineMemoryPeak] =
            CreatePerformanceMetricRow(memory, "引擎历史峰值", "Debug");
        _performanceMetricRows[PerformanceMetricManagedMemory] =
            CreatePerformanceMetricRow(memory, ".NET 托管堆", "不触发 GC");
        _performanceMetricRows[PerformanceMetricMessageBufferPeak] =
            CreatePerformanceMetricRow(memory, "消息缓冲峰值", "Deferred");
        _performanceMetricRows[PerformanceMetricVideoMemory] =
            CreatePerformanceMetricRow(memory, "显存总量", "纹理 + Buffer");
        _performanceMetricRows[PerformanceMetricTextureMemory] =
            CreatePerformanceMetricRow(memory, "纹理显存", "当前");
        _performanceMetricRows[PerformanceMetricBufferMemory] =
            CreatePerformanceMetricRow(memory, "Buffer 显存", "当前");

        TreeItem objects = CreatePerformanceMetricGroup(root, "对象");
        _performanceMetricRows[PerformanceMetricObjects] =
            CreatePerformanceMetricRow(objects, "Object", "包含 Node");
        _performanceMetricRows[PerformanceMetricResources] =
            CreatePerformanceMetricRow(objects, "Resource", "当前实例");
        _performanceMetricRows[PerformanceMetricNodes] =
            CreatePerformanceMetricRow(objects, "Node", "场景树");
        _performanceMetricRows[PerformanceMetricOrphans] =
            CreatePerformanceMetricRow(objects, "Orphan Node", "非零需检查");

        TreeItem rendering = CreatePerformanceMetricGroup(root, "渲染");
        _performanceMetricRows[PerformanceMetricRenderObjects] =
            CreatePerformanceMetricRow(rendering, "可见对象", "上一渲染帧");
        _performanceMetricRows[PerformanceMetricPrimitives] =
            CreatePerformanceMetricRow(rendering, "Primitive", "含额外 Pass");
        _performanceMetricRows[PerformanceMetricDrawCalls] =
            CreatePerformanceMetricRow(rendering, "Draw Call", "上一渲染帧");

        TreeItem physics2D = CreatePerformanceMetricGroup(root, "物理 2D");
        _performanceMetricRows[PerformanceMetricPhysics2DActive] =
            CreatePerformanceMetricRow(physics2D, "活动对象", "RigidBody2D");
        _performanceMetricRows[PerformanceMetricPhysics2DPairs] =
            CreatePerformanceMetricRow(physics2D, "碰撞对", "当前");
        _performanceMetricRows[PerformanceMetricPhysics2DIslands] =
            CreatePerformanceMetricRow(physics2D, "Island", "当前");

        TreeItem physics3D = CreatePerformanceMetricGroup(root, "物理 3D");
        _performanceMetricRows[PerformanceMetricPhysics3DActive] =
            CreatePerformanceMetricRow(physics3D, "活动对象", "RigidBody / Vehicle");
        _performanceMetricRows[PerformanceMetricPhysics3DPairs] =
            CreatePerformanceMetricRow(physics3D, "碰撞对", "当前");
        _performanceMetricRows[PerformanceMetricPhysics3DIslands] =
            CreatePerformanceMetricRow(physics3D, "Island", "当前");

        TreeItem pipelines = CreatePerformanceMetricGroup(root, "Pipeline");
        _performanceMetricRows[PerformanceMetricPipelineCanvas] =
            CreatePerformanceMetricRow(pipelines, "Canvas", "累计，只增不减");
        _performanceMetricRows[PerformanceMetricPipelineMesh] =
            CreatePerformanceMetricRow(pipelines, "Mesh", "加载阶段");
        _performanceMetricRows[PerformanceMetricPipelineSurface] =
            CreatePerformanceMetricRow(pipelines, "Surface", "可能产生卡顿");
        _performanceMetricRows[PerformanceMetricPipelineDraw] =
            CreatePerformanceMetricRow(pipelines, "Draw", "运行中卡顿风险");
        _performanceMetricRows[PerformanceMetricPipelineSpecialization] =
            CreatePerformanceMetricRow(pipelines, "Specialization", "后台优化");
    }

    private static TreeItem CreatePerformanceMetricGroup(TreeItem root, string title)
    {
        TreeItem group = root.CreateChild();
        group.SetText(0, title);
        group.SetSelectable(0, false);
        group.Collapsed = false;
        return group;
    }

    private static TreeItem CreatePerformanceMetricRow(
        TreeItem group,
        string metric,
        string note)
    {
        TreeItem row = group.CreateChild();
        row.SetText(1, metric);
        row.SetText(2, "0");
        row.SetText(3, note);
        row.SetTextAlignment(1, HorizontalAlignment.Left);
        row.SetTextAlignment(2, HorizontalAlignment.Right);
        row.SetTextAlignment(3, HorizontalAlignment.Left);
        return row;
    }

    private T GetPerformanceNode<T>(string path) where T : Node
    {
        T? node = _performanceDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerPerformance 场景缺少节点：{path}");
    }


    private void RefreshPerformanceDashboard()
    {
        if (!IsInstanceValid(_performanceFpsValue) ||
            !IsInstanceValid(_performanceProcessValue) ||
            !IsInstanceValid(_performancePhysicsValue) ||
            !IsInstanceValid(_performanceMemoryValue) ||
            !IsInstanceValid(_performanceManagedMemoryValue) ||
            !IsInstanceValid(_performanceFrameGraph) ||
            !IsInstanceValid(_performanceMemoryGraph) ||
            !IsInstanceValid(_performanceMetricsTree))
            return;

        double processSeconds = ReadPerformanceMonitor(Performance.Monitor.TimeProcess);
        double physicsSeconds = ReadPerformanceMonitor(Performance.Monitor.TimePhysicsProcess);
        double engineMemory = ReadPerformanceMonitor(Performance.Monitor.MemoryStatic);
        double managedMemory = GC.GetTotalMemory(forceFullCollection: false);
        double processMilliseconds = processSeconds * 1000d;
        double physicsMilliseconds = physicsSeconds * 1000d;

        _performanceFpsValue.Text =
            Mathf.RoundToInt(Engine.GetFramesPerSecond()).ToString(CultureInfo.InvariantCulture);
        _performanceProcessValue.Text = FormatMilliseconds(processMilliseconds);
        _performancePhysicsValue.Text = FormatMilliseconds(physicsMilliseconds);
        _performanceMemoryValue.Text = FormatBytes(engineMemory);
        _performanceManagedMemoryValue.Text = FormatBytes(managedMemory);

        AddPerformanceSample(
            processMilliseconds,
            physicsMilliseconds,
            engineMemory,
            managedMemory);
        UpdatePerformanceMetrics(engineMemory, managedMemory);
        _performanceFrameGraph.QueueRedraw();
        _performanceMemoryGraph.QueueRedraw();
    }

    private void RefreshSystemDashboard()
    {
        if (!IsInstanceValid(_systemUptimeValue) ||
            !IsInstanceValid(_systemDetailsTree))
            return;

        _systemUptimeValue.Text = FormatUptime(Time.GetTicksMsec());
        SetSystemValue(SystemMetricLocale, ReadCurrentLocale());
        SetSystemValue(SystemMetricWindowMode, ReadWindowMode());
        SetSystemValue(SystemMetricWindowSize, ReadWindowSize());
        SetSystemValue(SystemMetricScreenSize, ReadScreenSize());
        SetSystemValue(SystemMetricVsync, ReadVsyncMode());
    }

    private static string ReadCurrentLocale()
    {
        try
        {
            string locale = TranslationServer.GetLocale();
            return string.IsNullOrWhiteSpace(locale) ? "不可用" : locale;
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadWindowMode()
    {
        try
        {
            return DisplayServer.WindowGetMode((int)DisplayServer.MainWindowId) switch
            {
                DisplayServer.WindowMode.Windowed => "窗口",
                DisplayServer.WindowMode.Minimized => "最小化",
                DisplayServer.WindowMode.Maximized => "最大化",
                DisplayServer.WindowMode.Fullscreen => "全屏",
                DisplayServer.WindowMode.ExclusiveFullscreen => "独占全屏",
                _ => "不可用",
            };
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadWindowSize()
    {
        try
        {
            return FormatSize(DisplayServer.WindowGetSize((int)DisplayServer.MainWindowId));
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadScreenSize()
    {
        try
        {
            return FormatSize(DisplayServer.ScreenGetSize((int)DisplayServer.ScreenOfMainWindow));
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadVsyncMode()
    {
        try
        {
            return DisplayServer.WindowGetVsyncMode((int)DisplayServer.MainWindowId) switch
            {
                DisplayServer.VSyncMode.Disabled => "关闭",
                DisplayServer.VSyncMode.Enabled => "开启",
                DisplayServer.VSyncMode.Adaptive => "自适应",
                DisplayServer.VSyncMode.Mailbox => "Mailbox",
                _ => "不可用",
            };
        }
        catch
        {
            return "不可用";
        }
    }

    private static string FormatSize(Vector2I size)
    {
        return size.X > 0 && size.Y > 0
            ? $"{size.X.ToString(CultureInfo.InvariantCulture)} × " +
              size.Y.ToString(CultureInfo.InvariantCulture)
            : "不可用";
    }

    private static string FormatUptime(ulong milliseconds)
    {
        ulong totalSeconds = milliseconds / 1000UL;
        ulong days = totalSeconds / 86400UL;
        ulong hours = totalSeconds / 3600UL % 24UL;
        ulong minutes = totalSeconds / 60UL % 60UL;
        ulong seconds = totalSeconds % 60UL;
        return days > 0UL
            ? $"{days.ToString(CultureInfo.InvariantCulture)}d " +
              $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private void UpdatePerformanceMetrics(double engineMemory, double managedMemory)
    {
        SetPerformanceMetricValue(PerformanceMetricEngineMemory, FormatBytes(engineMemory));
        SetPerformanceMetricValue(
            PerformanceMetricEngineMemoryPeak,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.MemoryStaticMax)));
        SetPerformanceMetricValue(PerformanceMetricManagedMemory, FormatBytes(managedMemory));
        SetPerformanceMetricValue(
            PerformanceMetricMessageBufferPeak,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.MemoryMessageBufferMax)));
        SetPerformanceMetricValue(
            PerformanceMetricVideoMemory,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.RenderVideoMemUsed)));
        SetPerformanceMetricValue(
            PerformanceMetricTextureMemory,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.RenderTextureMemUsed)));
        SetPerformanceMetricValue(
            PerformanceMetricBufferMemory,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.RenderBufferMemUsed)));

        SetPerformanceMetricCount(
            PerformanceMetricObjects,
            Performance.Monitor.ObjectCount);
        SetPerformanceMetricCount(
            PerformanceMetricResources,
            Performance.Monitor.ObjectResourceCount);
        SetPerformanceMetricCount(
            PerformanceMetricNodes,
            Performance.Monitor.ObjectNodeCount);
        double orphanCount = ReadPerformanceMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        SetPerformanceMetricValue(
            PerformanceMetricOrphans,
            FormatCount(orphanCount),
            orphanCount > 0d ? new Color(1f, 0.72f, 0.28f) : null);

        SetPerformanceMetricCount(
            PerformanceMetricRenderObjects,
            Performance.Monitor.RenderTotalObjectsInFrame);
        SetPerformanceMetricCount(
            PerformanceMetricPrimitives,
            Performance.Monitor.RenderTotalPrimitivesInFrame);
        SetPerformanceMetricCount(
            PerformanceMetricDrawCalls,
            Performance.Monitor.RenderTotalDrawCallsInFrame);

        SetPerformanceMetricCount(
            PerformanceMetricPhysics2DActive,
            Performance.Monitor.Physics2DActiveObjects);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics2DPairs,
            Performance.Monitor.Physics2DCollisionPairs);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics2DIslands,
            Performance.Monitor.Physics2DIslandCount);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics3DActive,
            Performance.Monitor.Physics3DActiveObjects);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics3DPairs,
            Performance.Monitor.Physics3DCollisionPairs);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics3DIslands,
            Performance.Monitor.Physics3DIslandCount);

        SetPerformanceMetricCount(
            PerformanceMetricPipelineCanvas,
            Performance.Monitor.PipelineCompilationsCanvas);
        SetPerformanceMetricCount(
            PerformanceMetricPipelineMesh,
            Performance.Monitor.PipelineCompilationsMesh);
        SetPerformanceMetricCount(
            PerformanceMetricPipelineSurface,
            Performance.Monitor.PipelineCompilationsSurface);
        double drawPipelineCount =
            ReadPerformanceMonitor(Performance.Monitor.PipelineCompilationsDraw);
        SetPerformanceMetricValue(
            PerformanceMetricPipelineDraw,
            FormatCount(drawPipelineCount),
            drawPipelineCount > 0d ? new Color(1f, 0.72f, 0.28f) : null);
        SetPerformanceMetricCount(
            PerformanceMetricPipelineSpecialization,
            Performance.Monitor.PipelineCompilationsSpecialization);
    }

    private void SetPerformanceMetricCount(int index, Performance.Monitor monitor)
    {
        SetPerformanceMetricValue(index, FormatCount(ReadPerformanceMonitor(monitor)));
    }

    private void SetPerformanceMetricValue(int index, string value, Color? color = null)
    {
        TreeItem? row = _performanceMetricRows[index];
        if (row is null)
            return;

        row.SetText(2, value);
        if (color.HasValue)
            row.SetCustomColor(2, color.Value);
        else
            row.ClearCustomColor(2);
    }

    private void AddPerformanceSample(
        double processMilliseconds,
        double physicsMilliseconds,
        double engineMemory,
        double managedMemory)
    {
        int index = _performanceSampleWriteIndex;
        _performanceProcessSamples[index] = processMilliseconds;
        _performancePhysicsSamples[index] = physicsMilliseconds;
        _performanceEngineMemorySamples[index] = engineMemory;
        _performanceManagedMemorySamples[index] = managedMemory;
        _performanceSampleWriteIndex = (index + 1) % PerformanceSampleCapacity;
        if (_performanceSampleCount < PerformanceSampleCapacity)
            _performanceSampleCount++;
    }

    private void OnPerformanceFrameGraphDraw()
    {
        DrawPerformanceGraph(
            _performanceFrameGraph,
            _performanceProcessSamples,
            _performancePhysicsSamples,
            minimumMaximum: 16.667d,
            new Color(0.31f, 0.68f, 1f),
            new Color(0.65f, 0.49f, 1f),
            formatAsBytes: false);
    }

    private void OnPerformanceMemoryGraphDraw()
    {
        DrawPerformanceGraph(
            _performanceMemoryGraph,
            _performanceEngineMemorySamples,
            _performanceManagedMemorySamples,
            minimumMaximum: 1024d * 1024d,
            new Color(0.24f, 0.82f, 0.65f),
            new Color(1f, 0.68f, 0.28f),
            formatAsBytes: true);
    }

    private void DrawPerformanceGraph(
        Control? graph,
        double[] primarySamples,
        double[] secondarySamples,
        double minimumMaximum,
        Color primaryColor,
        Color secondaryColor,
        bool formatAsBytes)
    {
        if (!IsInstanceValid(graph))
            return;

        Vector2 size = graph.Size;
        const float verticalPadding = 4f;
        const float axisLabelWidth = 44f;
        const float latestValueWidth = 44f;
        float plotLeft = axisLabelWidth;
        float plotRight = Mathf.Max(plotLeft + 1f, size.X - latestValueWidth);
        float width = plotRight - plotLeft;
        float height = Mathf.Max(1f, size.Y - verticalPadding * 2f);
        Color gridColor = new(0.22f, 0.28f, 0.35f, 0.45f);
        for (int line = 0; line < 3; line++)
        {
            float y = verticalPadding + height * line / 2f;
            graph.DrawLine(
                new Vector2(plotLeft, y),
                new Vector2(plotRight, y),
                gridColor);
        }

        if (_performanceSampleCount == 0)
            return;

        double maximum = minimumMaximum;
        int startIndex = _performanceSampleCount < PerformanceSampleCapacity
            ? 0
            : _performanceSampleWriteIndex;
        for (int pointIndex = 0; pointIndex < _performanceSampleCount; pointIndex++)
        {
            int sampleIndex = (startIndex + pointIndex) % PerformanceSampleCapacity;
            maximum = Math.Max(maximum, primarySamples[sampleIndex]);
            maximum = Math.Max(maximum, secondarySamples[sampleIndex]);
        }

        float denominator = Math.Max(1, _performanceSampleCount - 1);
        for (int pointIndex = 0; pointIndex < _performanceSampleCount; pointIndex++)
        {
            int sampleIndex = (startIndex + pointIndex) % PerformanceSampleCapacity;
            float x = plotLeft + width * pointIndex / denominator;
            _performancePrimaryGraphPoints[pointIndex] = new Vector2(
                x,
                verticalPadding + height * (1f - (float)(primarySamples[sampleIndex] / maximum)));
            _performanceSecondaryGraphPoints[pointIndex] = new Vector2(
                x,
                verticalPadding + height * (1f - (float)(secondarySamples[sampleIndex] / maximum)));
        }

        if (_performanceSampleCount >= 2)
        {
            graph.DrawPolyline(
                _performancePrimaryGraphPoints.AsSpan(0, _performanceSampleCount),
                primaryColor,
                1.5f,
                antialiased: true);
            graph.DrawPolyline(
                _performanceSecondaryGraphPoints.AsSpan(0, _performanceSampleCount),
                secondaryColor,
                1.5f,
                antialiased: true);
        }

        DrawPerformanceGraphLabels(
            graph,
            maximum,
            primarySamples[(_performanceSampleWriteIndex - 1 + PerformanceSampleCapacity) %
                PerformanceSampleCapacity],
            secondarySamples[(_performanceSampleWriteIndex - 1 + PerformanceSampleCapacity) %
                PerformanceSampleCapacity],
            _performancePrimaryGraphPoints[_performanceSampleCount - 1].Y,
            _performanceSecondaryGraphPoints[_performanceSampleCount - 1].Y,
            plotLeft,
            plotRight,
            height,
            primaryColor,
            secondaryColor,
            formatAsBytes);
    }

    private static void DrawPerformanceGraphLabels(
        Control graph,
        double maximum,
        double primaryLatest,
        double secondaryLatest,
        float primaryY,
        float secondaryY,
        float plotLeft,
        float plotRight,
        float plotHeight,
        Color primaryColor,
        Color secondaryColor,
        bool formatAsBytes)
    {
        Font font = graph.GetThemeDefaultFont();
        int fontSize = Math.Clamp(graph.GetThemeDefaultFontSize(), 8, 9);
        Color axisColor = new(0.4f, 0.47f, 0.56f);
        float topBaseline = fontSize;
        float middleBaseline = 4f + plotHeight / 2f + fontSize * 0.35f;
        float bottomBaseline = graph.Size.Y - 2f;
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(0f, topBaseline),
            FormatPerformanceGraphValue(maximum, formatAsBytes),
            plotLeft - 6f,
            HorizontalAlignment.Right,
            fontSize,
            axisColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(0f, middleBaseline),
            FormatPerformanceGraphValue(maximum / 2d, formatAsBytes),
            plotLeft - 6f,
            HorizontalAlignment.Right,
            fontSize,
            axisColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(0f, bottomBaseline),
            FormatPerformanceGraphValue(0d, formatAsBytes),
            plotLeft - 6f,
            HorizontalAlignment.Right,
            fontSize,
            axisColor);

        float primaryBaseline = Mathf.Clamp(
            primaryY + fontSize * 0.35f,
            topBaseline,
            bottomBaseline);
        float secondaryBaseline = Mathf.Clamp(
            secondaryY + fontSize * 0.35f,
            topBaseline,
            bottomBaseline);
        float minimumSeparation = fontSize + 2f;
        if (Mathf.Abs(primaryBaseline - secondaryBaseline) < minimumSeparation)
        {
            float middle = (primaryBaseline + secondaryBaseline) / 2f;
            primaryBaseline = Mathf.Clamp(
                middle - minimumSeparation / 2f,
                topBaseline,
                bottomBaseline - minimumSeparation);
            secondaryBaseline = primaryBaseline + minimumSeparation;
        }

        float latestX = plotRight + 3f;
        float latestWidth = Mathf.Max(1f, graph.Size.X - latestX);
        graph.DrawLine(
            new Vector2(plotRight, primaryY),
            new Vector2(latestX - 1f, primaryBaseline - fontSize * 0.35f),
            primaryColor);
        graph.DrawLine(
            new Vector2(plotRight, secondaryY),
            new Vector2(latestX - 1f, secondaryBaseline - fontSize * 0.35f),
            secondaryColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(latestX, primaryBaseline),
            FormatPerformanceGraphValue(primaryLatest, formatAsBytes),
            latestWidth,
            HorizontalAlignment.Right,
            fontSize,
            primaryColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(latestX, secondaryBaseline),
            FormatPerformanceGraphValue(secondaryLatest, formatAsBytes),
            latestWidth,
            HorizontalAlignment.Right,
            fontSize,
            secondaryColor);
    }

    private static void DrawPerformanceGraphText(
        Control graph,
        Font font,
        Vector2 position,
        string text,
        float width,
        HorizontalAlignment alignment,
        int fontSize,
        Color color)
    {
        graph.DrawString(font, position, text, alignment, width, fontSize, color);
    }

    private static string FormatPerformanceGraphValue(double value, bool formatAsBytes)
    {
        return formatAsBytes
            ? FormatBytes(value)
            : FormatMilliseconds(value);
    }

    private static double ReadPerformanceMonitor(Performance.Monitor monitor)
    {
        return Math.Max(0d, Performance.GetMonitor(monitor));
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return $"{milliseconds.ToString("0.00", CultureInfo.InvariantCulture)} ms";
    }

    private static string FormatCount(double value)
    {
        return Math.Round(Math.Max(0d, value))
            .ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatBytes(double bytes)
    {
        double value = Math.Max(0d, bytes);
        string unit = "B";
        if (value >= 1024d)
        {
            value /= 1024d;
            unit = "KiB";
        }
        if (value >= 1024d)
        {
            value /= 1024d;
            unit = "MiB";
        }
        if (value >= 1024d)
        {
            value /= 1024d;
            unit = "GiB";
        }

        string format = value >= 100d ? "0" : value >= 10d ? "0.0" : "0.00";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {unit}";
    }


}
#endif
