using System;
using System.Collections.Generic;
using IsoRPG.Localization;
using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.Quests
{
    /// <summary>Где квест находится в жизни игрока.</summary>
    public enum QuestState
    {
        /// <summary>Не взят: его ещё предлагают.</summary>
        Available,

        /// <summary>Взят, но цель не выполнена.</summary>
        Active,

        /// <summary>Цель выполнена, награда не получена.</summary>
        ReadyToTurnIn,

        /// <summary>Сдан и закрыт.</summary>
        Completed,
    }

    /// <summary>
    /// Журнал квестов игрока.
    ///
    /// Прогресс считается по СУМКЕ, а не по числу убийств. Разница
    /// принципиальная: убить и подобрать — два разных действия, и квест «принеси
    /// пять костей» должен требовать обоих. Иначе награда приходит за то, что
    /// игрок и так делал бы, а поход за добычей теряет смысл.
    ///
    /// Побочный выигрыш: счётчик всегда честен. Выбросил кости — счётчик упал,
    /// и никакого рассинхрона между «сколько засчитано» и «что лежит в сумке»
    /// быть не может, потому что второе и есть первое.
    /// </summary>
    public sealed class QuestLog : MonoBehaviour
    {
        [SerializeField] private List<QuestDefinition> known = new List<QuestDefinition>();

        private readonly Dictionary<QuestDefinition, QuestState> states =
            new Dictionary<QuestDefinition, QuestState>();

        private Inventory inventory;
        private Experience experience;

        /// <summary>Что-то изменилось: взяли, сдали, счётчик сдвинулся.</summary>
        public event Action Changed;

        public IReadOnlyList<QuestDefinition> Known => known;

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            experience = GetComponent<Experience>();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += OnInventoryChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
        }

        public QuestState StateOf(QuestDefinition quest)
        {
            if (quest == null) return QuestState.Completed;
            return states.TryGetValue(quest, out var state) ? state : QuestState.Available;
        }

        public bool IsActive(QuestDefinition quest)
        {
            var state = StateOf(quest);
            return state == QuestState.Active || state == QuestState.ReadyToTurnIn;
        }

        /// <summary>Сколько нужного уже в сумке.</summary>
        public int Progress(QuestDefinition quest)
        {
            if (quest == null || quest.requiredItem == null || inventory == null) return 0;
            return inventory.CountOf(quest.requiredItem);
        }

        /// <summary>Взятое и сданное — для сохранения.</summary>
        public List<IsoRPG.Save.SavedQuest> CaptureState()
        {
            var result = new List<IsoRPG.Save.SavedQuest>();

            foreach (var quest in known)
            {
                if (quest == null) continue;

                result.Add(new IsoRPG.Save.SavedQuest
                {
                    quest = quest.name,
                    state = (int)StateOf(quest),
                });
            }

            return result;
        }

        public void RestoreState(List<IsoRPG.Save.SavedQuest> saved)
        {
            known.Clear();
            states.Clear();

            var database = IsoRPG.Save.GameDatabase.Instance;
            if (saved == null || database == null) return;

            foreach (var entry in saved)
            {
                var quest = database.Quest(entry.quest);
                if (quest == null) continue;

                var state = (QuestState)entry.state;

                // Доступный квест не запоминаем: он и так предлагается всем,
                // у кого его нет. В журнале ему до взятия делать нечего.
                if (state == QuestState.Available) continue;

                known.Add(quest);
                states[quest] = state;
            }

            Changed?.Invoke();
        }

        public void Accept(QuestDefinition quest)
        {
            if (quest == null || StateOf(quest) != QuestState.Available) return;

            states[quest] = QuestState.Active;
            if (!known.Contains(quest)) known.Add(quest);

            CombatLog.Add(Loc.F("Взят квест: {0}", Loc.T(quest.title)), LogKind.Loot);

            // Сразу пересчитываем: нужное могло уже лежать в сумке, и квест
            // выполнен в момент взятия. Редкий случай, но без пересчёта
            // счётчик показал бы ноль при полной сумке.
            Recheck();
        }

        /// <summary>
        /// Сдать квест: забрать предметы, выдать награду.
        /// </summary>
        public bool TurnIn(QuestDefinition quest) => TurnIn(quest, null);

        /// <summary>
        /// Сдать квест, взяв одну вещь из наград на выбор.
        /// </summary>
        /// <param name="chosen">
        /// Что выбрал игрок. Null — выбора не было или квест его не
        /// предлагает. Чужой предмет не принимаем: список наград задан
        /// квестом, и подставить туда что угодно нельзя.
        /// </param>
        public bool TurnIn(QuestDefinition quest, ItemDefinition chosen)
        {
            if (quest == null || StateOf(quest) != QuestState.ReadyToTurnIn) return false;
            if (inventory == null) return false;

            bool hasChoices = quest.rewardChoices != null && quest.rewardChoices.Length > 0;

            if (hasChoices)
            {
                if (chosen == null)
                {
                    CombatLog.Add("Сначала выбери награду", LogKind.System);
                    return false;
                }

                bool ours = false;
                foreach (var option in quest.rewardChoices)
                    if (option == chosen) { ours = true; break; }

                if (!ours)
                {
                    Debug.LogWarning("[IsoRPG] Выбрана награда не из списка квеста — отклонено.");
                    return false;
                }
            }

            // Сначала проверяем место под награду, потом забираем предметы.
            // Иначе можно отдать кости и не получить кинжал — потеря, которую
            // игрок нам не простит и будет прав.
            if (quest.rewardItem != null && !inventory.HasFreeSlot() &&
                inventory.CountOf(quest.requiredItem) > quest.requiredCount)
            {
                CombatLog.Add("Нет места в сумке для награды", LogKind.System);
                return false;
            }

            inventory.Remove(quest.requiredItem, quest.requiredCount);

            if (quest.rewardItem != null)
                inventory.Add(new ItemStack(quest.rewardItem, quest.rewardCount));

            if (chosen != null)
            {
                inventory.Add(new ItemStack(chosen, 1));
                CombatLog.Add(Loc.F("Награда: {0}", Loc.T(chosen.displayName)), LogKind.Loot);
            }

            if (quest.rewardGold > 0) inventory.AddGold(quest.rewardGold);

            if (quest.rewardExperience > 0 && experience != null)
                experience.AddExperience(quest.rewardExperience);

            states[quest] = QuestState.Completed;

            CombatLog.Add(Loc.F("Квест выполнен: {0}", Loc.T(quest.title)), LogKind.Loot);
            Changed?.Invoke();

            return true;
        }

        // ------------------------------------------------------------------

        private void OnInventoryChanged() => Recheck();

        /// <summary>
        /// Пересчитывает состояние активных квестов по содержимому сумки.
        /// </summary>
        private void Recheck()
        {
            bool changed = false;

            // Копия ключей: состояние меняется внутри цикла, а словарь
            // менять во время обхода нельзя.
            var active = new List<QuestDefinition>(states.Keys);

            foreach (var quest in active)
            {
                var state = states[quest];
                if (state != QuestState.Active && state != QuestState.ReadyToTurnIn) continue;

                bool done = Progress(quest) >= quest.requiredCount;
                var wanted = done ? QuestState.ReadyToTurnIn : QuestState.Active;

                if (states[quest] == wanted) continue;

                states[quest] = wanted;
                changed = true;

                if (wanted == QuestState.ReadyToTurnIn)
                    CombatLog.Add(Loc.F("Цель выполнена: {0} — вернись к заказчику", Loc.T(quest.title)),
                                  LogKind.Loot);
            }

            // Событие шлём всегда: счётчик в панели меняется и без смены
            // состояния, а он тоже подписан.
            Changed?.Invoke();

            if (changed) { /* состояние сменилось, маркеры над NPC обновятся сами */ }
        }
    }
}
