using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Items;
using IsoRPG.Progression;
using IsoRPG.Quests;

namespace IsoRPG.Save
{
    /// <summary>
    /// Справочник всего, на что может ссылаться сохранение.
    ///
    /// В файл нельзя записать ссылку на ассет — только его имя. При загрузке
    /// по этому имени надо найти сам предмет, талант или квест, а искать в
    /// сцене нечего: половина из них в этот момент нигде не лежит.
    ///
    /// Живёт в папке Resources и грузится по имени, без единой ссылки из
    /// сцены. Иначе справочник пришлось бы протаскивать в каждый компонент,
    /// который что-то сохраняет, — а это почти все.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "IsoRPG/Справочник")]
    public sealed class GameDatabase : ScriptableObject
    {
        private const string ResourcePath = "GameDatabase";

        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();
        [SerializeField] private List<TalentDefinition> talents = new List<TalentDefinition>();
        [SerializeField] private List<QuestDefinition> quests = new List<QuestDefinition>();

        private Dictionary<string, ItemDefinition> itemsByName;
        private Dictionary<string, TalentDefinition> talentsByName;
        private Dictionary<string, QuestDefinition> questsByName;

        private static GameDatabase cached;

        /// <summary>
        /// Справочник. Грузится один раз и живёт до конца игры: он неизменен,
        /// а обращаются к нему в цикле по каждой ячейке сумки.
        /// </summary>
        public static GameDatabase Instance
        {
            get
            {
                if (cached != null) return cached;

                cached = Resources.Load<GameDatabase>(ResourcePath);

                if (cached == null)
                    Debug.LogError("[IsoRPG] Нет справочника в Resources/" + ResourcePath +
                                   " — сохранение не сможет восстановить предметы. " +
                                   "Прогони Tools/IsoRPG/Собрать справочник.");

                return cached;
            }
        }

        public void Setup(List<ItemDefinition> allItems,
                          List<TalentDefinition> allTalents,
                          List<QuestDefinition> allQuests)
        {
            items = allItems;
            talents = allTalents;
            quests = allQuests;

            itemsByName = null;
            talentsByName = null;
            questsByName = null;
        }

        public ItemDefinition Item(string key) => Find(ref itemsByName, items, key);
        public TalentDefinition Talent(string key) => Find(ref talentsByName, talents, key);
        public QuestDefinition Quest(string key) => Find(ref questsByName, quests, key);

        public IReadOnlyList<QuestDefinition> AllQuests => quests;

        // ------------------------------------------------------------------

        private static T Find<T>(ref Dictionary<string, T> map, List<T> source, string key)
            where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (map == null)
            {
                map = new Dictionary<string, T>();

                foreach (var entry in source)
                {
                    if (entry == null) continue;
                    map[entry.name] = entry;
                }
            }

            if (map.TryGetValue(key, out var found)) return found;

            // Молчать нельзя: пропавший предмет выглядит как «сохранение
            // потерялось», хотя потерялась одна ссылка. Чаще всего это
            // переименованный ассет.
            Debug.LogWarning("[IsoRPG] В справочнике нет записи «" + key + "» (" + typeof(T).Name +
                             "). Ассет переименован или удалён?");

            return null;
        }
    }
}
