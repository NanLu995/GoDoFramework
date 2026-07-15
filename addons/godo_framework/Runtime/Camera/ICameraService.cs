#nullable enable

namespace GoDo;

/// <summary>面向业务层的主镜头注册结果查询、激活与恢复服务。</summary>
public interface ICameraService
{
    /// <summary>当前已激活的主镜头 ID；没有活动镜头时为 null。</summary>
    CameraId? ActivePrimary { get; }

    /// <summary>激活指定主镜头；切换成功后记录此前的不同镜头以供恢复。</summary>
    void ActivatePrimary(CameraId id);

    /// <summary>恢复最近一个仍然有效的主镜头；没有可恢复镜头时返回 false。</summary>
    bool RestorePreviousPrimary();
}
