using System;
using System.IO;
using UnityEngine;

namespace IsoRPG.Save
{
    /// <summary>
    /// Куда уходит состояние.
    ///
    /// Существует ради одного будущего события: когда игра станет сетевой,
    /// запись должна уехать на сервер, и переписывать при этом всё, что
    /// собирает состояние, нельзя. Здесь меняется транспорт, а слой выше
    /// остаётся прежним.
    ///
    /// Асинхронность заложена сразу — не потому, что файл её требует, а
    /// потому что сервер потребует. Код, написанный в расчёте на мгновенный
    /// ответ, при переезде на сеть переписывается целиком.
    /// </summary>
    public interface ISaveBackend
    {
        void Write(SaveFile data, Action<bool> done = null);

        void Read(Action<SaveFile> done);

        bool HasSave { get; }

        void Erase();
    }

    /// <summary>
    /// Запись в файл рядом с игрой.
    ///
    /// Пишем через временный файл и подмену: если игру закрыть посреди
    /// записи, старое сохранение останется целым. Прямая запись в тот же файл
    /// в этот момент оставила бы обрубок, и потерян был бы весь прогресс, а
    /// не последняя минута.
    /// </summary>
    public sealed class FileSaveBackend : ISaveBackend
    {
        private const string FileName = "character.json";

        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);
        private static string TempPath => Path + ".tmp";

        public bool HasSave => File.Exists(Path);

        public void Write(SaveFile data, Action<bool> done = null)
        {
            try
            {
                data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string json = JsonUtility.ToJson(data, prettyPrint: true);

                File.WriteAllText(TempPath, json);

                if (File.Exists(Path)) File.Delete(Path);
                File.Move(TempPath, Path);

                done?.Invoke(true);
            }
            catch (Exception error)
            {
                // Сохранение не должно ронять игру. Но и молчать нельзя:
                // тихо не сохраняющаяся игра — худший из возможных багов,
                // потому что обнаруживается он только потерей прогресса.
                Debug.LogError("[IsoRPG] Не удалось сохранить: " + error.Message);
                done?.Invoke(false);
            }
        }

        public void Read(Action<SaveFile> done)
        {
            if (!HasSave)
            {
                done?.Invoke(null);
                return;
            }

            try
            {
                string json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<SaveFile>(json);

                done?.Invoke(data);
            }
            catch (Exception error)
            {
                Debug.LogError("[IsoRPG] Сохранение повреждено: " + error.Message);
                done?.Invoke(null);
            }
        }

        public void Erase()
        {
            if (File.Exists(Path)) File.Delete(Path);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }

        /// <summary>Где лежит файл — пригодится, когда что-то пойдёт не так.</summary>
        public static string Location => Path;
    }
}
