namespace GoDo;

/// <summary>面向业务层的多槽位可靠存档服务。所有操作都是同步 Godot 文件 I/O，只能在 GoDoRuntime 所在的主线程调用。</summary>
public interface ISaveService
{
    /// <summary>编码并保存一个槽位；失败时抛出 SaveException。</summary>
    /// <typeparam name="T">业务存档模型类型。</typeparam>
    /// <param name="slot">通过 <see cref="SaveSlot.Create"/> 创建的有效槽位。</param>
    /// <param name="value">交给 <paramref name="codec"/> 编码的业务值。</param>
    /// <param name="dataVersion">大于 0、由业务 Codec 管理的 Payload 版本。</param>
    /// <param name="codec">负责编码当前业务格式的 Codec。</param>
    /// <exception cref="System.ArgumentException"><paramref name="slot"/> 未通过 <see cref="SaveSlot.Create"/> 初始化。</exception>
    /// <exception cref="System.ArgumentNullException"><paramref name="codec"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="dataVersion"/> 不大于 0。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    /// <exception cref="SaveException">编码、Payload 上限、目录、写入、校验、备份或提交失败。</exception>
    void Save<T>(SaveSlot slot, T value, int dataVersion, ISaveCodec<T> codec);

    /// <summary>读取、校验并解码槽位；主文件损坏时尝试备份。</summary>
    /// <typeparam name="T">业务存档模型类型。</typeparam>
    /// <param name="slot">通过 <see cref="SaveSlot.Create"/> 创建的有效槽位。</param>
    /// <param name="codec">负责解码文件版本并迁移业务模型的 Codec。</param>
    /// <returns>正式档、备份或未找到状态；未找到不是异常。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="slot"/> 未通过 <see cref="SaveSlot.Create"/> 初始化。</exception>
    /// <exception cref="System.ArgumentNullException"><paramref name="codec"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    /// <exception cref="SaveException">存在文件但正式档和备份都无法读取、校验或解码。</exception>
    SaveLoadResult<T> Load<T>(SaveSlot slot, ISaveCodec<T> codec);

    /// <summary>检查正式存档或备份是否存在；不读取或验证文件内容。</summary>
    /// <param name="slot">通过 <see cref="SaveSlot.Create"/> 创建的有效槽位。</param>
    /// <returns>正式文件或备份至少存在一个时为 <see langword="true"/>；仅有临时文件时仍为 <see langword="false"/>。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="slot"/> 未通过 <see cref="SaveSlot.Create"/> 初始化。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    bool Exists(SaveSlot slot);

    /// <summary>尝试删除正式文件、备份和临时文件；任一失败时不会恢复已经删除的文件。</summary>
    /// <param name="slot">通过 <see cref="SaveSlot.Create"/> 创建的有效槽位。</param>
    /// <returns>至少删除一个文件时为 <see langword="true"/>；三类文件都不存在时为 <see langword="false"/>。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="slot"/> 未通过 <see cref="SaveSlot.Create"/> 初始化。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    /// <exception cref="SaveException">一个或多个现有文件删除失败；其他文件可能已经成功删除。</exception>
    bool Delete(SaveSlot slot);
}
