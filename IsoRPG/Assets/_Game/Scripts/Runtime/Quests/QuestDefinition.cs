using UnityEngine;
using IsoRPG.Items;

namespace IsoRPG.Quests
{
    /// <summary>
    /// Описание квеста: что предлагают, что нужно принести, что дадут.
    ///
    /// Ассетом, а не кодом, по той же причине, что способности и предметы:
    /// квестов будет много, они почти целиком состоят из текста и чисел, и
    /// править их должен человек, а не программист. Добавить квест — создать
    /// ассет, а не написать класс.
    /// </summary>
    [CreateAssetMenu(fileName = "Q_NewQuest", menuName = "IsoRPG/Квест")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [Header("Тексты")]
        public string title = "Название квеста";

        [Tooltip("Уровень задания. Показывается в скобках перед названием, как в WoW: по нему игрок решает, по зубам ли оно.")]
        public int level = 1;

        [Tooltip("Зона, к которой относится задание. Заголовок группы в панели отслеживания.")]
        public string zone = "Колдридж-Вэлли";

        [TextArea(3, 6)]
        [Tooltip("Что говорит NPC, предлагая работу.")]
        public string offerText = "";

        [TextArea(2, 4)]
        [Tooltip("Что говорит, когда работа ещё не сделана.")]
        public string inProgressText = "Возвращайся, когда закончишь.";

        [TextArea(2, 4)]
        [Tooltip("Что говорит, принимая выполненную работу.")]
        public string completeText = "Ты справился. Держи, что обещано.";

        [Header("Цель")]
        [Tooltip("Что нужно принести.")]
        public ItemDefinition requiredItem;

        [Tooltip("Сколько штук.")]
        public int requiredCount = 5;

        [Header("Награда")]
        /// <summary>
        /// Награда на выбор. Игрок берёт ОДНУ вещь из этого списка при сдаче.
        ///
        /// Отдельно от <see cref="rewardItem"/>, а не вместо: гарантированная
        /// награда и выбор — разные обещания, и в одном квесте могут быть оба.
        /// Пусто — выбора нет, работает как раньше.
        /// </summary>
        public ItemDefinition[] rewardChoices = new ItemDefinition[0];

        public ItemDefinition rewardItem;
        public int rewardCount = 1;
        public int rewardExperience = 250;
        public int rewardGold = 0;

        /// <summary>
        /// Строка цели для панели отслеживания: «Кости скелета 3/5».
        /// </summary>
        public string ObjectiveLine(int have)
        {
            string itemName = requiredItem != null ? requiredItem.displayName : "предмет";
            return itemName + "  " + Mathf.Min(have, requiredCount) + " / " + requiredCount;
        }
    }
}
