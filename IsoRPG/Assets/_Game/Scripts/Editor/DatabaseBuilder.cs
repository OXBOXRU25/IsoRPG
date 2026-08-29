using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using IsoRPG.Items;
using IsoRPG.Progression;
using IsoRPG.Quests;
using IsoRPG.Save;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает справочник всего, на что ссылается сохранение.
    ///
    /// Складывает в Resources — единственную папку, из которой ассет
    /// достаётся в игре по имени, без ссылки из сцены. Иначе справочник
    /// пришлось бы протаскивать в каждый компонент, который что-то
    /// сохраняет, а это почти все.
    ///
    /// Пересобирать надо после КАЖДОГО нового предмета, таланта или квеста:
    /// чего нет в справочнике, того не будет и в загруженной игре. Поэтому
    /// сборщик сцены зовёт его сам.
    /// </summary>
    public static class DatabaseBuilder
    {
        private const string Folder = "Assets/_Game/Resources";
        private const string AssetPath = Folder + "/GameDatabase.asset";

        [MenuItem("Tools/IsoRPG/Собрать справочник", priority = 3)]
        public static GameDatabase Build()
        {
            EnsureFolder(Folder);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetPath);

            if (database == null)
            {
                database = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(database, AssetPath);
            }

            var items = Collect<ItemDefinition>("Assets/_Game/Data/Items");
            var talents = Collect<TalentDefinition>("Assets/_Game/Data/Talents");
            var quests = Collect<QuestDefinition>("Assets/_Game/Data/Quests");

            database.Setup(items, talents, quests);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Справочник собран: предметов " + items.Count +
                      ", талантов " + talents.Count + ", квестов " + quests.Count + ".");

            return database;
        }

        private static List<T> Collect<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new List<T>();

            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)

                // По имени: порядок поиска в проекте непостоянен, а нам важно,
                // чтобы список не менялся сам собой от пересборки к пересборке —
                // иначе каждая правка справочника выглядит в истории как
                // осмысленное изменение.
                .OrderBy(asset => asset.name)
                .ToList();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
