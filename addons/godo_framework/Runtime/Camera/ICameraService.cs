#nullable enable

namespace GoDo;

/// <summary>面向业务层的主镜头注册结果查询、激活与恢复服务。</summary>
public interface ICameraService
{
    /// <summary>当前仍然有效的主镜头 ID；没有活动镜头或其 Rig 已失效时为 null。</summary>
    /// <exception cref="System.InvalidOperationException">当前调用不在 GoDoRuntime 记录的 Godot 主线程。</exception>
    CameraId? ActivePrimary { get; }

    /// <summary>激活指定主镜头；完整切换成功后记录此前的不同镜头实例以供恢复。</summary>
    /// <param name="id">已经注册的非默认镜头 ID。</param>
    /// <exception cref="System.InvalidOperationException">当前调用不在 GoDoRuntime 记录的 Godot 主线程。</exception>
    /// <exception cref="System.ArgumentException"><paramref name="id"/> 是默认的空 ID。</exception>
    /// <exception cref="CameraOperationException">
    /// 没有可用的匹配 Rig，或目标激活、当前镜头停用失败。目标激活失败时当前镜头保持不变；
    /// 当前镜头停用失败时会尝试停用目标以回滚。
    /// </exception>
    void ActivatePrimary(CameraId id);

    /// <summary>恢复最近一个仍注册且有效的具体主镜头实例，并跳过失效历史。</summary>
    /// <returns>成功恢复一个历史镜头时为 <see langword="true"/>；没有可恢复实例时为 <see langword="false"/>。</returns>
    /// <exception cref="System.InvalidOperationException">当前调用不在 GoDoRuntime 记录的 Godot 主线程。</exception>
    /// <exception cref="CameraOperationException">历史目标激活或当前镜头停用失败；失败项仍保留在恢复历史中。</exception>
    bool RestorePreviousPrimary();
}
