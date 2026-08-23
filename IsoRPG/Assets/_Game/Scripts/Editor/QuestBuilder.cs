using UnityEditor;
using UnityEngine;
using IsoRPG.Quests;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Создаёт квесты как ассеты.
    ///
    /// Тем же способом, что способности и предметы: текст и числа живут в
    /// файле, который правится в инспекторе. Квест — это в первую очередь
    /// текст, и держать его в коде значит требовать компиляции ради запятой.
    /// </summary>
    public static class QuestBuilder
    {
        private const string Folder = "Assets/_Game/Data/Quests";
        private const string FirstQuest = "Q_BonesForBlade";

        [MenuItem("Tools/IsoRPG/Создать квесты", priority = 33)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            EnsureFolder(Folder);

            string path = Folder + "/" + FirstQuest + ".asset";
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);

            bool created = quest == null;

            if (created)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, path);
            }

            // Тексты и числа задаём только при создании: если человек правил
            // их в инспекторе, повторный вызов не должен затирать правки.
            if (created)
            {
                quest.title = "Кости для клинка";

                quest.offerText =
                    "Хочешь эпический кинжал?\n\n" +
                    "Тогда сходи к руинам и принеси мне пять костей скелетов. " +
                    "Не спрашивай зачем. Принесёшь — получишь клинок, какого " +
                    "у тебя ещё не было.";

                quest.inProgressText =
                    "Пять костей. Возвращайся, когда наберёшь.";

                quest.completeText =
                    "Все пять. Не думал, что справишься.\n\n" +
                    "Держи, что обещано. Он твой.";

                quest.requiredCount = 5;
                quest.rewardCount = 1;
                quest.rewardExperience = 320;
                quest.rewardGold = 50;
            }

            // Ссылки на предметы проставляем всегда: предметы могли быть
            // пересозданы, и ссылка на удалённый ассет становится пустой.
            quest.requiredItem = ItemsBuilder.LoadItem("I_SkeletonBone");
            quest.rewardItem = ItemsBuilder.LoadItem("I_ShadowfangDagger");

            if (quest.requiredItem == null || quest.rewardItem == null)
                Debug.LogError("[IsoRPG] Не найдены предметы квеста — прогони " +
                               "Tools/IsoRPG/Создать предметы и добычу.");

            EditorUtility.SetDirty(quest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Квест " + (created ? "создан" : "обновлён") + ": " + quest.title);
        }

        /// <summary>Первый квест — им пользуется сборщик сцены.</summary>
        public static QuestDefinition LoadFirst() =>
            AssetDatabase.LoadAssetAtPath<QuestDefinition>(Folder + "/" + FirstQuest + ".asset");

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder)
                .Replace(System.IO.Path.DirectorySeparatorChar, '/');
            string leaf = System.IO.Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
