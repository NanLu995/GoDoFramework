using System;
using Godot;

#nullable enable

namespace GoDo.Editor;

/// <summary>GoDoRuntime Autoload 的显式安装与健康检查插件。</summary>
[Tool]
public sealed partial class GoDoEditorPlugin : EditorPlugin
{
    private const string MenuItemName = "GoDo Framework...";
    private const string AutoloadName = "GoDoRuntime";
    private const string AutoloadSetting = "autoload/GoDoRuntime";
    private const string RuntimeScenePath = "res://addons/godo_framework/Core/GoDoRuntime.tscn";

    private AcceptDialog? _setupDialog;
    private ConfirmationDialog? _uninstallDialog;
    private Button? _checkButton;
    private Button? _installButton;
    private Button? _uninstallButton;

    public override void _EnterTree()
    {
        CreateDialogs();
        AddToolMenuItem(MenuItemName, Callable.From(OpenSetupDialog));
    }

    public override void _ExitTree()
    {
        RemoveToolMenuItem(MenuItemName);

        if (IsInstanceValid(_checkButton))
            _checkButton.Pressed -= OnCheckPressed;
        if (IsInstanceValid(_installButton))
            _installButton.Pressed -= OnInstallPressed;
        if (IsInstanceValid(_uninstallButton))
            _uninstallButton.Pressed -= OnUninstallPressed;
        if (IsInstanceValid(_uninstallDialog))
            _uninstallDialog.Confirmed -= OnUninstallConfirmed;

        if (IsInstanceValid(_setupDialog))
            _setupDialog.QueueFree();
        if (IsInstanceValid(_uninstallDialog))
            _uninstallDialog.QueueFree();

        _setupDialog = null;
        _uninstallDialog = null;
        _checkButton = null;
        _installButton = null;
        _uninstallButton = null;
    }

    private void CreateDialogs()
    {
        _setupDialog = new AcceptDialog
        {
            Title = "GoDo Framework",
            OkButtonText = "关闭",
            MinSize = new Vector2I(620, 360)
        };
        _checkButton = _setupDialog.AddButton("重新检查", true);
        _installButton = _setupDialog.AddButton("安装 Runtime", true);
        _uninstallButton = _setupDialog.AddButton("卸载 Runtime", true);
        _checkButton.Pressed += OnCheckPressed;
        _installButton.Pressed += OnInstallPressed;
        _uninstallButton.Pressed += OnUninstallPressed;
        EditorInterface.Singleton.GetBaseControl().AddChild(_setupDialog);

        _uninstallDialog = new ConfirmationDialog
        {
            Title = "卸载 GoDoRuntime",
            DialogText = "只会移除正确匹配的 GoDoRuntime Autoload，不会删除任何框架或业务文件。是否继续？",
            OkButtonText = "卸载",
            CancelButtonText = "取消"
        };
        _uninstallDialog.Confirmed += OnUninstallConfirmed;
        EditorInterface.Singleton.GetBaseControl().AddChild(_uninstallDialog);
    }

    private void OpenSetupDialog()
    {
        RefreshReport();
        _setupDialog!.PopupCentered(new Vector2I(620, 360));
    }

    private void OnCheckPressed()
    {
        RefreshReport();
    }

    private void OnInstallPressed()
    {
        try
        {
            GoDoHealthReport report = CheckHealth();
            if (report.AutoloadHealthy && !report.HasDuplicate)
            {
                ShowMessage("GoDoRuntime 已正确安装，无需重复操作。");
                return;
            }

            if (!report.CanInstall)
            {
                ShowMessage("当前状态不能安全安装。请先根据检查结果解决缺失、冲突或重复注册问题。");
                return;
            }

            AddAutoloadSingleton(AutoloadName, RuntimeScenePath);
            GoDoHealthReport result = RefreshReport();
            ShowMessage(result.AutoloadHealthy
                ? "GoDoRuntime 安装成功。"
                : "安装调用已完成，但复查未通过。请查看健康检查结果和编辑器输出。");
        }
        catch (Exception exception)
        {
            ReportException("安装 GoDoRuntime 失败", exception);
        }
    }

    private void OnUninstallPressed()
    {
        GoDoHealthReport report = CheckHealth();
        if (!report.AutoloadHealthy)
        {
            RefreshReport();
            ShowMessage("当前 GoDoRuntime Autoload 不存在或路径不匹配，插件不会执行卸载。");
            return;
        }

        _uninstallDialog!.PopupCentered();
    }

    private void OnUninstallConfirmed()
    {
        try
        {
            if (!HasExpectedAutoload())
            {
                RefreshReport();
                ShowMessage("Autoload 状态已变化，已取消卸载。");
                return;
            }

            RemoveAutoloadSingleton(AutoloadName);
            GoDoHealthReport result = RefreshReport();
            ShowMessage(result.AutoloadMissing
                ? "GoDoRuntime Autoload 已卸载，框架文件未删除。"
                : "卸载调用已完成，但复查未通过。请查看健康检查结果和编辑器输出。");
        }
        catch (Exception exception)
        {
            ReportException("卸载 GoDoRuntime 失败", exception);
        }
    }

    private GoDoHealthReport RefreshReport()
    {
        GoDoHealthReport report = CheckHealth();
        _setupDialog!.DialogText = report.Format();
        _installButton!.Disabled = !report.CanInstall;
        _uninstallButton!.Disabled = !report.AutoloadHealthy;
        return report;
    }

    private static GoDoHealthReport CheckHealth()
    {
        var report = new GoDoHealthReport();
        CheckVersion(report);
        CheckRuntimeScene(report);
        CheckAutoload(report);
        return report;
    }

    private static void CheckVersion(GoDoHealthReport report)
    {
        Godot.Collections.Dictionary version = Engine.GetVersionInfo();
        int major = version["major"].AsInt32();
        int minor = version["minor"].AsInt32();
        report.VersionSupported = major == 4 && minor >= 7;
        report.Add(
            report.VersionSupported ? GoDoHealthLevel.Normal : GoDoHealthLevel.Error,
            "Godot 版本",
            report.VersionSupported ? $"{major}.{minor}" : $"当前为 {major}.{minor}，需要 Godot 4.7 或更高的 4.x 版本");
    }

    private static void CheckRuntimeScene(GoDoHealthReport report)
    {
        if (!ResourceLoader.Exists(RuntimeScenePath))
        {
            report.Add(GoDoHealthLevel.Error, "Runtime 场景", $"缺少 {RuntimeScenePath}");
            return;
        }

        PackedScene? scene = ResourceLoader.Load<PackedScene>(RuntimeScenePath);
        report.RuntimeSceneValid = scene != null && scene.CanInstantiate();
        report.Add(
            report.RuntimeSceneValid ? GoDoHealthLevel.Normal : GoDoHealthLevel.Error,
            "Runtime 场景",
            report.RuntimeSceneValid ? "存在且可实例化" : "资源存在，但不是可实例化的 PackedScene");
    }

    private static void CheckAutoload(GoDoHealthReport report)
    {
        report.AutoloadMissing = !ProjectSettings.HasSetting(AutoloadSetting);
        if (report.AutoloadMissing)
        {
            report.Add(GoDoHealthLevel.Warning, "Autoload", "尚未安装 GoDoRuntime");
        }
        else
        {
            string actualPath = NormalizeAutoloadPath(
                ProjectSettings.GetSetting(AutoloadSetting).AsString());
            report.AutoloadHealthy = actualPath == RuntimeScenePath;
            report.HasNameConflict = !report.AutoloadHealthy;
            report.Add(
                report.AutoloadHealthy ? GoDoHealthLevel.Normal : GoDoHealthLevel.Error,
                "Autoload",
                report.AutoloadHealthy
                    ? $"{AutoloadName} → {RuntimeScenePath}"
                    : $"名称 {AutoloadName} 已指向其他路径：{actualPath}");
        }

        var projectConfig = new ConfigFile();
        Error loadError = projectConfig.Load("res://project.godot");
        if (loadError != Error.Ok)
        {
            report.Add(GoDoHealthLevel.Error, "重复注册", $"无法读取 project.godot：{loadError}");
            return;
        }

        string[] autoloadNames = projectConfig.GetSectionKeys("autoload");
        for (int i = 0; i < autoloadNames.Length; i++)
        {
            string name = autoloadNames[i];
            if (name == AutoloadName)
                continue;

            string path = NormalizeAutoloadPath(
                projectConfig.GetValue("autoload", name).AsString());
            if (path != RuntimeScenePath)
                continue;

            report.HasDuplicate = true;
            report.Add(
                GoDoHealthLevel.Error,
                "重复注册",
                $"{name} 也指向 {RuntimeScenePath}");
        }

        if (!report.HasDuplicate)
            report.Add(GoDoHealthLevel.Normal, "重复注册", "未发现");
    }

    private static bool HasExpectedAutoload()
    {
        if (!ProjectSettings.HasSetting(AutoloadSetting))
            return false;

        return NormalizeAutoloadPath(
            ProjectSettings.GetSetting(AutoloadSetting).AsString()) == RuntimeScenePath;
    }

    private static string NormalizeAutoloadPath(string path) =>
        path.StartsWith('*') ? path[1..] : path;

    private void ShowMessage(string message)
    {
        _setupDialog!.DialogText = $"{message}\n\n{CheckHealth().Format()}";
    }

    private void ReportException(string message, Exception exception)
    {
        GD.PushError($"[GoDo Editor] {message}: {exception}");
        ShowMessage($"{message}：{exception.Message}");
    }
}
