using System;
using System.Collections.Generic;

namespace IsoRPG.Save
{
    /// <summary>
    /// Состояние ПЕРСОНАЖА: уровень, добро, изученное, взятое.
    ///
    /// Отделено от состояния мира намеренно. В мультиплеере это две разные
    /// вещи и живут они в разных местах: персонаж привязан к учётной записи и
    /// лежит на сервере, а мир общий для всех или создаётся заново на каждый
    /// заход. Смешать их сейчас — значит разбирать потом, когда данных станет
    /// втрое больше.
    ///
    /// Всё хранится ИМЕНАМИ ассетов, а не ссылками: ссылку в файл не
    /// записать, а по имени справочник находит нужный предмет обратно.
    /// </summary>
    [Serializable]
    public sealed class CharacterState
    {
        public int level = 1;
        public int experience;

        public int health;
        public int energy;

        public int gold;

        /// <summary>Сумка по ячейкам. Пустые тоже записываем: порядок важен.</summary>
        public List<SavedStack> bag = new List<SavedStack>();

        public List<SavedEquip> worn = new List<SavedEquip>();
        public List<SavedTalent> talents = new List<SavedTalent>();
        public List<SavedQuest> quests = new List<SavedQuest>();

        /// <summary>Где стоял. Возвращаться в чистое поле после выхода неприятно.</summary>
        public float x, y, z;
    }

    /// <summary>
    /// Состояние МИРА: что в нём уже произошло.
    ///
    /// Сейчас это горстка флагов, и хочется положить их к персонажу. Но в
    /// мультиплеере открытый сундук — свойство мира, а не игрока, и тогда эти
    /// данные уедут в другое хранилище целиком.
    /// </summary>
    [Serializable]
    public sealed class WorldState
    {
        /// <summary>Уникальные награды, которые уже выданы.</summary>
        public List<string> claimedRewards = new List<string>();

        /// <summary>Открытые сундуки.</summary>
        public List<string> openedChests = new List<string>();
    }

    /// <summary>Всё вместе — то, что уходит в файл или на сервер.</summary>
    [Serializable]
    public sealed class SaveFile
    {
        /// <summary>
        /// Версия формата. Записывается всегда, читается при загрузке:
        /// когда состав данных изменится, старый файл надо будет либо
        /// перевести, либо честно отбросить — и знать, какой он, обязательно.
        /// </summary>
        public int version = 1;

        public string savedAt = "";

        public CharacterState character = new CharacterState();
        public WorldState world = new WorldState();
    }

    [Serializable]
    public struct SavedStack
    {
        public string item;
        public int count;
    }

    [Serializable]
    public struct SavedEquip
    {
        public int slot;
        public string item;
    }

    [Serializable]
    public struct SavedTalent
    {
        public string talent;
        public int rank;
    }

    [Serializable]
    public struct SavedQuest
    {
        public string quest;
        public int state;
    }
}
