using System;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理主内容场景的异步加载与替换。
/// <para>本服务必须位于场景树中，并且所有调用都必须发生在 Godot 主线程。</para>
/// </summary>
public sealed partial class SceneService : Node, ISceneService
{
    private ResourceLoadOperation<PackedScene>? _loadOperation;
    private int _lifecycleVersion;

    /// <summary>当前是否正在切换场景。</summary>
    public bool IsChanging { get; private set; }

    /// <summary>当前场景加载进度，范围为 0 到 1。</summary>
    public float Progress { get; private set; }

    public override void _EnterTree()
    {
        _lifecycleVersion++;
    }

    public override void _ExitTree()
    {
        _lifecycleVersion++;

        if (_loadOperation != null)
            _loadOperation.ProgressChanged -= OnLoadProgressChanged;
    }

    /// <summary>
    /// 异步加载并替换当前主场景。加载或实例化失败时保留旧场景。
    /// </summary>
    /// <exception cref="InvalidOperationException">服务未进入场景树，或已有切换正在执行。</exception>
    /// <exception cref="SceneChangeException">加载、实例化或挂载目标场景失败。</exception>
    public async Task<Node> ChangeAsync(ResourceKey key)
    {
        MainThreadGuard.VerifyAccess();

        if (!IsInsideTree())
            throw new InvalidOperationException("SceneService 必须进入场景树后才能切换场景。");

        if (IsChanging)
            throw new InvalidOperationException("已有场景切换正在执行，不能重复发起请求。");

        SceneTree tree = GetTree();
        Node? currentScene = tree.CurrentScene;
        if (IsInstanceValid(currentScene) &&
            (currentScene == this || currentScene.IsAncestorOf(this)))
        {
            throw new InvalidOperationException(
                "SceneService 不能挂在当前主场景内部，否则切换时会随旧场景一起释放。");
        }

        IsChanging = true;
        Progress = 0f;
        int lifecycleVersion = _lifecycleVersion;

        try
        {
            _loadOperation = ResourceHub.LoadAsync<PackedScene>(key);
            _loadOperation.ProgressChanged += OnLoadProgressChanged;

            PackedScene packedScene = await _loadOperation.Completion;
            MainThreadGuard.VerifyAccess();
            VerifyLifecycle(lifecycleVersion, key);

            Node newScene = InstantiateScene(packedScene, key);
            ReplaceCurrentScene(newScene, key, lifecycleVersion);
            Progress = 1f;
            return newScene;
        }
        catch (Exception exception) when (exception is not SceneChangeException)
        {
            throw new SceneChangeException(
                key,
                $"场景切换失败，旧场景保持不变: {key.Value}",
                exception);
        }
        finally
        {
            if (_loadOperation != null)
                _loadOperation.ProgressChanged -= OnLoadProgressChanged;

            _loadOperation = null;
            IsChanging = false;
        }
    }

    private static Node InstantiateScene(PackedScene packedScene, ResourceKey key)
    {
        if (!packedScene.CanInstantiate())
        {
            throw new SceneChangeException(
                key,
                $"目标 PackedScene 不包含可实例化的节点: {key.Value}");
        }

        try
        {
            return packedScene.Instantiate();
        }
        catch (Exception exception)
        {
            throw new SceneChangeException(
                key,
                $"目标 PackedScene 无法实例化: {key.Value}",
                exception);
        }
    }

    private void ReplaceCurrentScene(Node newScene, ResourceKey key, int lifecycleVersion)
    {
        SceneTree tree = GetTree();
        Node? oldScene = tree.CurrentScene;

        try
        {
            tree.Root.AddChild(newScene);
            VerifyLifecycle(lifecycleVersion, key);
            tree.CurrentScene = newScene;
        }
        catch (Exception exception)
        {
            if (IsInstanceValid(newScene))
                newScene.QueueFree();

            throw new SceneChangeException(
                key,
                $"目标场景无法加入场景树: {key.Value}",
                exception);
        }

        if (IsInstanceValid(oldScene) && oldScene != newScene)
            oldScene.QueueFree();
    }

    private void VerifyLifecycle(int expectedVersion, ResourceKey key)
    {
        if (IsInsideTree() && _lifecycleVersion == expectedVersion)
            return;

        throw new SceneChangeException(
            key,
            $"SceneService 在加载完成前退出或重新进入了场景树，已取消切换: {key.Value}",
            new OperationCanceledException("SceneService 生命周期已变化。"));
    }

    private void OnLoadProgressChanged(float progress)
    {
        Progress = progress;
    }
}
