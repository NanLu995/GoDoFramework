namespace GoDo;

/// <summary>面向业务层的多槽位可靠存档服务。</summary>
public interface ISaveService
{
    /// <summary>编码并保存一个槽位；失败时抛出 SaveException。</summary>
    void Save<T>(SaveSlot slot, T value, int dataVersion, ISaveCodec<T> codec);

    /// <summary>读取、校验并解码槽位；主文件损坏时尝试备份。</summary>
    SaveLoadResult<T> Load<T>(SaveSlot slot, ISaveCodec<T> codec);

    /// <summary>检查正式存档或备份是否存在。</summary>
    bool Exists(SaveSlot slot);

    /// <summary>删除正式文件、备份和临时文件；没有文件时返回 false。</summary>
    bool Delete(SaveSlot slot);
}
