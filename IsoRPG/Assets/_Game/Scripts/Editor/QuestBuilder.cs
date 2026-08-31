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
        private const string BoarQuest = "Q_BoarHunt";

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

            BoarHunt();
        }

        /// <summary>
        /// Охота на кабанов — квест дозорного Талина Кини.
        ///
        /// Считается по клыкам в сумке, а не по счётчику убийств: прогресс
        /// квестов у нас читается из сумки, и заводить вторую механику ради
        /// одного задания значит держать два разных ответа на вопрос «сколько
        /// сделано». Клык падает с каждого кабана гарантированно, так что
        /// «убей двенадцать» и «принеси двенадцать клыков» — одно и то же
        /// действие, просто первое звучит как охота, а второе как учёт.
        /// </summary>
        private static void BoarHunt()
        {
            string path = Folder + "/" + BoarQuest + ".asset";
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);

            bool created = quest == null;

            if (created)
            {
                quest = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(quest, path);

                quest.title = "Слишком много кабанов";

                quest.offerText =
                    "Что может быть лучше дня охоты на кабана, а?\n\n" +
                    "Хотя здесь, в Колдридж-Вэлли, кабанов столько, что это почти " +
                    "лишает охоту всякого удовольствия. В последнее время их в округе " +
                    "развелось так много, что мне и выходить-то опасно каждый день.\n\n" +
                    "Короче говоря, буду признателен, если поможешь мне проредить стадо. " +
                    "Двенадцати хватит. Клыки приноси — по ним и посчитаем.";

                quest.inProgressText =
                    "Двенадцать клыков, охотник. Кабаны сами себя не убавят.";

                quest.completeText =
                    "Двенадцать. Ну вот, теперь и до ручья дойти можно, не оглядываясь.\n\n" +
                    "Держи за труды. И заходи, если снова расплодятся.";

                quest.requiredCount = 12;
                quest.rewardCount = 1;
                quest.rewardGold = 85;
            }

            // Числа Павлона от 01.09.2026: 250 опыта, награда на выбор из
            // двух штанов. Ставим ВСЕГДА, а не только при создании: квест уже
            // лежал ассетом с прежними числами, и правка «только при
            // создании» до него бы не дошла.
            quest.rewardExperience = 250;

            // Текст сдачи — от Павлона, с именем героя. Механики создания
            // персонажа пока нет, поэтому имя записано прямо: Шико.
            quest.completeText =
                "Превосходно! Теперь я могу вернуться к своей... неспешной... охоте. " +
                "Спасибо, Шико.";

            quest.requiredItem = ItemsBuilder.LoadItem("I_BoarTusk");

            // Гарантированной награды нет — только выбор.
            quest.rewardItem = null;

            quest.rewardChoices = new[]
            {
                ItemsBuilder.LoadItem("I_LeatherBreeches"),
                ItemsBuilder.LoadItem("I_ClothPantaloons"),
            };

            if (quest.requiredItem == null)
                Debug.LogError("[IsoRPG] Нет предмета I_BoarTusk — прогони «Создать предметы и добычу».");

            foreach (var choice in quest.rewardChoices)
                if (choice == null)
                    Debug.LogError("[IsoRPG] Одна из наград на выбор не найдена — " +
                                   "игрок не сможет сдать квест.");

            EditorUtility.SetDirty(quest);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Квест " + (created ? "создан" : "обновлён") + ": " + quest.title);
        }

        /// <summary>Квест дозорного. Нужен сборщику NPC.</summary>
        public static QuestDefinition LoadBoarHunt() =>
            AssetDatabase.LoadAssetAtPath<QuestDefinition>(Folder + "/" + BoarQuest + ".asset");

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
