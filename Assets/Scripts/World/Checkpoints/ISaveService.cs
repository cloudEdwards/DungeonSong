namespace DungeonSong.World
{
    /// <summary>
    /// Persistence, separated from what is being persisted.
    /// <para>
    /// Rest points call <see cref="Save"/> and never learn where the bytes go. Swapping
    /// JSON-on-disk for cloud saves or a slot system is one new implementation.
    /// </para>
    /// </summary>
    public interface ISaveService
    {
        bool HasSave { get; }

        void Save(SaveData data);

        /// <summary>Returns the stored save, or null when there is none.</summary>
        SaveData Load();

        void Delete();
    }
}
