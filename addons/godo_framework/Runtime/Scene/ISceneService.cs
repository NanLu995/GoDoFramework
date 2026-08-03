using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的主内容场景切换服务。</summary>
public interface ISceneService
{
    /// <summary>当前是否正在切换场景。</summary>
    bool IsChanging { get; }

    /// <summary>当前加载进度，范围为 0 到 1；失败或取消后复位为 0。</summary>
    float Progress { get; }

    /// <summary>
    /// 异步加载并替换当前主场景；服务离树时取消当前等待，但不取消 ResourceHub 的共享底层加载。
    /// </summary>
    /// <param name="key">目标 PackedScene 的资源键。</param>
    /// <returns>成功提交并成为 CurrentScene 的新场景节点。</returns>
    /// <exception cref="InvalidOperationException">服务未进入场景树，或已有场景切换正在执行。</exception>
    /// <exception cref="SceneChangeException">场景加载、实例化、提交或服务生命周期发生失败。</exception>
    Task<Node> ChangeAsync(ResourceKey key);

    /// <summary>
    /// 异步加载并替换当前主场景，同时报告该次请求的加载进度并允许调用方在提交前取消。
    /// <para>
    /// 取消不会中止 ResourceHub 中可能共享的底层加载；一旦开始同步提交新场景，取消不再撤销提交。
    /// </para>
    /// </summary>
    /// <param name="key">目标 PackedScene 的资源键。</param>
    /// <param name="onProgress">可选进度回调，范围为 0 到 1，由 Godot 主线程调用。</param>
    /// <param name="cancellationToken">调用方取消标记。</param>
    /// <returns>成功提交并成为 CurrentScene 的新场景节点。</returns>
    /// <exception cref="InvalidOperationException">服务未进入场景树，或已有场景切换正在执行。</exception>
    /// <exception cref="OperationCanceledException">调用方在场景提交前取消请求。</exception>
    /// <exception cref="SceneChangeException">场景加载、实例化、提交或服务生命周期发生失败。</exception>
    Task<Node> ChangeAsync(
        ResourceKey key,
        Action<float>? onProgress,
        CancellationToken cancellationToken = default);
}
