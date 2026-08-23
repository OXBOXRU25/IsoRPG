using System;
using System.Collections.Generic;
using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Progression
{
    /// <summary>
    /// Что игрок вложил в таланты и что ему за это причитается.
    ///
    /// Единственный источник правды: бой не хранит у себя прибавок, а
    /// спрашивает их здесь в момент расчёта. Иначе прибавку надо было бы
    /// вносить и снимать при каждом изменении — а это ровно тот код, в
    /// котором заводятся вечные +5% от таланта, который давно сброшен.
    ///
    /// Очки: одно за уровень, начиная со второго. Первый уровень их не даёт —
    /// нечего выбирать, когда способность всего одна.
    /// </summary>
    public sealed class TalentBook : MonoBehaviour
    {
        [Tooltip("Все таланты дерева. Заполняет сборщик сцены.")]
        [SerializeField] private List<TalentDefinition> talents = new List<TalentDefinition>();

        private readonly Dictionary<TalentDefinition, int> ranks =
            new Dictionary<TalentDefinition, int>();

        private Experience experience;

        public event Action Changed;

        public IReadOnlyList<TalentDefinition> All => talents;

        /// <summary>Всего очков за уровни.</summary>
        public int TotalPoints => experience != null ? Mathf.Max(0, experience.Level - 1) : 0;

        public int SpentPoints
        {
            get
            {
                int sum = 0;
                foreach (var pair in ranks) sum += pair.Value;

                return sum;
            }
        }

        public int AvailablePoints => Mathf.Max(0, TotalPoints - SpentPoints);

        public void Setup(List<TalentDefinition> list)
        {
            talents = list;
            ranks.Clear();
        }

        private void Awake()
        {
            experience = GetComponent<Experience>();
        }

        private void OnEnable()
        {
            if (experience != null) experience.LevelUp += OnLevelUp;
        }

        private void OnDisable()
        {
            if (experience != null) experience.LevelUp -= OnLevelUp;
        }

        // ------------------------------------------------------------------

        public int RankOf(TalentDefinition talent)
        {
            if (talent == null) return 0;

            return ranks.TryGetValue(talent, out int rank) ? rank : 0;
        }

        /// <summary>Сколько очков вложено в ветку — от этого зависят ярусы.</summary>
        public int SpentIn(TalentBranch branch)
        {
            int sum = 0;

            foreach (var pair in ranks)
                if (pair.Key != null && pair.Key.branch == branch) sum += pair.Value;

            return sum;
        }

        /// <summary>
        /// Можно ли вложить очко. Отдельно от Learn, потому что окну надо
        /// показать серым то, что нельзя, — до того, как игрок нажмёт.
        /// </summary>
        public bool CanLearn(TalentDefinition talent, out string reason)
        {
            reason = "";

            if (talent == null) return false;

            if (AvailablePoints <= 0)
            {
                reason = "Нет свободных очков";
                return false;
            }

            if (RankOf(talent) >= talent.maxRank)
            {
                reason = "Изучен полностью";
                return false;
            }

            int inBranch = SpentIn(talent.branch);
            if (inBranch < talent.RequiredInBranch)
            {
                reason = "Нужно " + talent.RequiredInBranch + " очков в ветке, вложено " + inBranch;
                return false;
            }

            return true;
        }

        public bool Learn(TalentDefinition talent)
        {
            if (!CanLearn(talent, out _)) return false;

            ranks[talent] = RankOf(talent) + 1;

            CombatLog.Add("Изучено: " + talent.displayName + " (" +
                          ranks[talent] + " из " + talent.maxRank + ")", LogKind.System);

            Changed?.Invoke();
            return true;
        }

        /// <summary>Что вложено — для сохранения.</summary>
        public List<IsoRPG.Save.SavedTalent> CaptureState()
        {
            var result = new List<IsoRPG.Save.SavedTalent>();

            foreach (var pair in ranks)
            {
                if (pair.Key == null || pair.Value <= 0) continue;

                result.Add(new IsoRPG.Save.SavedTalent
                {
                    talent = pair.Key.name,
                    rank = pair.Value,
                });
            }

            return result;
        }

        /// <summary>
        /// Вернуть вложенное напрямую, минуя проверки доступности.
        ///
        /// Проверки нужны, когда игрок тратит очко: они следят за ярусами и
        /// остатком. При загрузке всё это проверено когда-то, а порядок
        /// восстановления произвольный — идти через Learn значило бы
        /// отказывать в талантах верхних ярусов, пока не лягут нижние.
        /// </summary>
        public void RestoreState(List<IsoRPG.Save.SavedTalent> saved)
        {
            ranks.Clear();

            var database = IsoRPG.Save.GameDatabase.Instance;

            if (saved != null && database != null)
            {
                foreach (var entry in saved)
                {
                    var talent = database.Talent(entry.talent);
                    if (talent == null) continue;

                    ranks[talent] = Mathf.Clamp(entry.rank, 0, talent.maxRank);
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Сбросить всё. Без этого дерево — билет в один конец, а на этапе,
        /// когда баланс ещё щупают руками, это просто вредно.
        /// </summary>
        public void ResetAll()
        {
            if (ranks.Count == 0) return;

            ranks.Clear();
            CombatLog.Add("Таланты сброшены.", LogKind.System);

            Changed?.Invoke();
        }

        /// <summary>
        /// Суммарная прибавка от всех талантов с этим действием.
        /// Ноль, если ни один не вложен, — и это самый частый ответ.
        /// </summary>
        public float Bonus(TalentEffect effect)
        {
            float sum = 0f;

            foreach (var pair in ranks)
            {
                if (pair.Key == null || pair.Key.effect != effect) continue;

                sum += pair.Key.ValueAt(pair.Value);
            }

            return sum;
        }

        // ------------------------------------------------------------------

        private void OnLevelUp(int level)
        {
            CombatLog.Add("Получено очко талантов. Всего свободных: " +
                          AvailablePoints, LogKind.System);

            Changed?.Invoke();
        }
    }
}
