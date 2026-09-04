using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Манекен, которого нельзя убить.
    ///
    /// Здоровье возвращается через полсекунды после последнего удара. Именно
    /// возвращается, а не стоит на месте: полоска над головой должна дёрнуться
    /// от попадания, иначе непонятно, попал ты или махнул мимо — а смотреть
    /// боевые анимации приходится по многу раз подряд, и живая цель для этого
    /// не годится.
    ///
    /// Компонент нарочно тупой: ни ИИ, ни ответа, ни смерти. Всё, что делает
    /// манекен манекеном, снимается заданием `dummy` при сборке.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class DummyHeal : MonoBehaviour
    {
        [Tooltip("Через сколько секунд после удара здоровье возвращается.")]
        [SerializeField] private float after = 0.5f;

        /// <summary>
        /// Запас здоровья манекена.
        ///
        /// Павлон 04.09.2026: «манекен убивается, сделай ему в 10 раз больше
        /// хп». Со стандартной сотней серия сильных ударов успевала снять всё
        /// до того, как отработает лечение через полсекунды.
        ///
        /// Ставим здесь, а не в сборщике сцены: лечит его этот же компонент,
        /// и держать запас отдельно от лечения значит завести одно число в
        /// двух местах.
        /// </summary>
        [Tooltip("Запас здоровья. Манекен должен переживать любую серию ударов.")]
        [SerializeField] private int hitPoints = 1000;

        private Health health;
        private float healAt;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Setup(hitPoints);
            health.Damaged += OnDamaged;
        }

        /// <summary>
        /// Встать рядом с живым героем, уже в игре.
        ///
        /// Ставить манекен в сцене оказалось бесполезно: игра грузит
        /// сохранение, и герой появляется совсем в другом месте — а кукла
        /// остаётся там, где он стоял в редакторе, то есть над землёй.
        /// Три попытки подряд я чинил высоту, хотя ломалось не место, а
        /// момент: я считал позицию раньше, чем герой встал на своё.
        ///
        /// Здесь же мир уже загружен и герой на месте, поэтому и земля
        /// находится честно — лучом вниз, мимо собственных коллайдеров.
        /// </summary>
        private void Start()
        {
            var hero = Object.FindFirstObjectByType<IsoRPG.Player.PlayerInputRouter>();
            if (hero == null) return;

            // Место манекена НЕ трогаем.
            //
            // Павлон 04.09.2026: «при каждом заходе игры он смещается куда-то
            // со старого места, поставь его на 1 место, чтобы он никуда не
            // девался больше». Причина была ровно в этом коде: он ставил
            // манекен в трёх метрах перед героем при каждом запуске, а герой
            // после загрузки сохранения появляется то тут, то там — вместе с
            // ним ездил и манекен.
            //
            // Теперь место задаётся один раз в редакторе, заданием `dummy`, и
            // живёт в сцене. Мишень должна стоять там, где её оставили: к ней
            // привыкают, как к предмету обстановки.
            //
            // Досадку на землю оставляем: она правит только высоту и только
            // первые две секунды, а без неё манекен висит над травой, если
            // земля под ним оказалась ниже, чем в редакторе.
            // Место, выбранное игроком клавишей F7, переживает перезапуск.
            if (PlayerPrefs.HasKey(SpotKey + ".x"))
            {
                transform.position = new Vector3(
                    PlayerPrefs.GetFloat(SpotKey + ".x"),
                    PlayerPrefs.GetFloat(SpotKey + ".y"),
                    PlayerPrefs.GetFloat(SpotKey + ".z"));

                Debug.Log($"[IsoRPG] Манекен на своём месте: {transform.position}");
            }

            settleUntil = Time.time + 2f;

            // Оружие пробуем взять не один раз, а первые несколько секунд.
            //
            // Герой надевает своё при загрузке сохранения, и порядок запуска
            // компонентов этого не гарантирует: манекен просыпается раньше,
            // видит у героя пустые руки и честно копирует пустоту. Павлон
            // 04.09.2026: «кинжалов у манекена нет».
            armUntil = Time.time + 5f;
            armHero = hero.gameObject;
        }

        /// <summary>
        /// Выдать манекену то же оружие, что сейчас в руках у героя.
        ///
        /// Просьба Павла 04.09.2026: «дай ему тоже два кинжала». Делается в
        /// ИГРЕ, а не в редакторе: там у экипировки не вызван Awake, ссылка на
        /// сумку внутри неё пустая, и надевание молча возвращает false —
        /// задание честно доложило «клинков в руках 0».
        ///
        /// Оружие СПРАШИВАЕМ у героя, а не задаём своим списком: сменит он
        /// кинжалы на что-то другое — манекен возьмёт то же самое, и второй
        /// список чинить не придётся.
        /// </summary>
        private void Arm(GameObject hero)
        {
            var mine = GetComponent<IsoRPG.Items.Equipment>();
            var bag = GetComponent<IsoRPG.Items.Inventory>();
            var his = hero.GetComponent<IsoRPG.Items.Equipment>();

            if (mine == null || bag == null || his == null)
            {
                Debug.Log($"[IsoRPG] Манекен без снаряжения: экипировка {(mine != null)}, " +
                          $"сумка {(bag != null)}, у героя {(his != null)} — кинжалы не выданы.");
                return;
            }

            // Уже что-то в сумке или в руках — второй раз не выдаём.
            //
            // Поломка 04.09.2026, моя: повторные попытки раз в полсекунды
            // добавляли по клинку КАЖДЫЙ раз, и за пять секунд сумка
            // набивалась двумя десятками кинжалов. Павлон увидел это в игре.
            // Повтор нужен был только чтобы дождаться, пока герой оденется, —
            // значит и проверять надо, не выдали ли уже.
            foreach (var have in bag.All())
                if (!have.IsEmpty && have.Item != null && have.Item.IsEquippable) return;

            if (!mine.IsEmpty(IsoRPG.Items.EquipSlot.MainHand)) return;

            int armed = 0;

            foreach (var slot in new[] { IsoRPG.Items.EquipSlot.MainHand, IsoRPG.Items.EquipSlot.OffHand })
            {
                var stack = his.GetSlot(slot);

                var item = stack.Item;

                // У героя пусто — берём кинжал из его СУМКИ.
                //
                // Проверка сохранения 04.09.2026 показала неожиданное: у
                // героя `worn` пуст, ничего не надето вовсе, а два ржавых
                // кинжала лежат в сумке. Павлон угадал причину с первого
                // раза: «а может потому что на герое не надеты кинжалы?»
                //
                // Поэтому спрашиваем героя двумя способами: сперва руки,
                // потом сумку. Своего списка предметов здесь по-прежнему нет —
                // манекен носит то же, что и он.
                if (item == null) item = FirstWeaponInBag(hero);

                if (item == null) continue;

                bag.Add(item, 1);
                armed++;
            }

            // Надеваем всё, что легло в сумку: экипировка сама разложит по
            // рукам, а мы не гадаем про номера ячеек.
            // Слушаем отказ: экипировка объясняет, почему не надела, а до
            // сих пор я видел только «надето 0» и гадал.
            void Refused(IsoRPG.Items.ItemDefinition item, string why) =>
                Debug.LogWarning($"[IsoRPG] Манекену не надеть «{item?.name}»: {why}");

            mine.Rejected += Refused;

            // Надеваем по СОДЕРЖИМОМУ ячейки, а не по её номеру.
            //
            // Прежняя версия брала слоты 0 и 1 — а там лежат клыки и шкуры,
            // приехавшие вместе с копией сумки героя. Отсюда девять строк
            // «в сумку легло 2, надето 0»: кинжалы ложились в первые
            // свободные ячейки где-то дальше, а надеть пытались мусор.
            int worn = 0;
            int index = 0;

            foreach (var stack in bag.All())
            {
                if (worn >= armed) break;

                if (!stack.IsEmpty && stack.Item != null && stack.Item.IsEquippable &&
                    (stack.Item.slot == IsoRPG.Items.EquipSlot.MainHand ||
                     stack.Item.slot == IsoRPG.Items.EquipSlot.OffHand))
                {
                    if (mine.EquipFromInventory(index)) worn++;
                }

                index++;
            }

            mine.Rejected -= Refused;

            Debug.Log($"[IsoRPG] Манекен: в сумку легло {armed}, надето {worn}.");

            // Получилось — больше не пробуем: иначе он наберёт полную сумку
            // копий за пять секунд повторов.
            if (worn > 0) armHero = null;
        }

        /// <summary>
        /// Досаживать манекен на землю первые две секунды.
        ///
        /// Один замер в Start не годится: герой в этот миг ещё не осел —
        /// капсула опускает его несколько кадров после загрузки сохранения,
        /// и высота, взятая у него сразу, оказывается на ладонь выше земли.
        /// Павлон 04.09.2026: «персонаж на земле, а манекен над травой».
        ///
        /// Две секунды и раз в четверть секунды: это разовая посадка, а не
        /// работа в кадре — после неё компонент к земле не возвращается.
        /// </summary>
        private float settleUntil;
        private float nextSettle;

        private void Settle()
        {
            if (Time.time > settleUntil || Time.time < nextSettle) return;

            nextSettle = Time.time + 0.25f;

            var own = GetComponentsInChildren<Collider>(true);
            foreach (var c in own) c.enabled = false;

            bool found = Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
                                         out var hit, 8f, ~0, QueryTriggerInteraction.Ignore);

            foreach (var c in own) c.enabled = true;

            if (found) transform.position = hit.point;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(int amount, GameObject source) => healAt = Time.time + after;

        /// <summary>
        /// Первое оружие ближнего боя в сумке героя.
        ///
        /// Запасной путь: руки у него могут быть пусты, а кинжалы лежать в
        /// сумке — так и оказалось в сохранении 04.09.2026.
        /// </summary>
        private static IsoRPG.Items.ItemDefinition FirstWeaponInBag(GameObject hero)
        {
            var bag = hero.GetComponent<IsoRPG.Items.Inventory>();

            if (bag == null) return null;

            foreach (var stack in bag.All())
            {
                if (stack.IsEmpty || stack.Item == null) continue;
                if (!stack.Item.IsEquippable) continue;

                if (stack.Item.slot == IsoRPG.Items.EquipSlot.MainHand ||
                    stack.Item.slot == IsoRPG.Items.EquipSlot.OffHand)
                    return stack.Item;
            }

            return null;
        }

        /// <summary>До какого времени пробуем скопировать оружие героя и у кого.</summary>
        private float armUntil;
        private GameObject armHero;
        private float nextArm;

        /// <summary>Где стоит манекен. Ключ хранилища — своё место для мишени.</summary>
        private const string SpotKey = "dummy.spot";

        /// <summary>
        /// Поставить манекен туда, где сейчас стоит герой, и запомнить навсегда.
        ///
        /// Павлон 04.09.2026: «поставь где-то недалеко от лошади и зафиксируй
        /// там». Лошадь по имени в сцене не находится — мир автора грузится не
        /// из неё, — а угадывать координаты вслепую значит промахнуться и
        /// гонять сборку по кругу.
        ///
        /// Поэтому место выбирает он сам: подошёл куда надо, нажал F7 —
        /// манекен встал там и остаётся там при всех следующих заходах.
        /// Хранится в настройках игрока, а не в сцене: сцену пересобирает
        /// задание, и место каждый раз затиралось бы.
        /// </summary>
        private void PlaceHere(GameObject hero)
        {
            Vector3 at = hero.transform.position + hero.transform.forward * 3f;

            transform.position = at;
            transform.rotation = Quaternion.LookRotation(-hero.transform.forward);

            PlayerPrefs.SetFloat(SpotKey + ".x", at.x);
            PlayerPrefs.SetFloat(SpotKey + ".y", at.y);
            PlayerPrefs.SetFloat(SpotKey + ".z", at.z);
            PlayerPrefs.Save();

            settleUntil = Time.time + 2f;

            Debug.Log($"[IsoRPG] Манекен переставлен и закреплён: {at}");
        }

        private void Update()
        {
            Settle();

            // F7 — «поставь мишень здесь».
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f7Key.wasPressedThisFrame)
            {
                var hero = Object.FindFirstObjectByType<IsoRPG.Player.PlayerInputRouter>();
                if (hero != null) PlaceHere(hero.gameObject);
            }

            if (armHero != null && Time.time < armUntil && Time.time >= nextArm)
            {
                nextArm = Time.time + 0.5f;
                Arm(armHero);
            }

            if (healAt <= 0f || Time.time < healAt) return;

            healAt = 0f;

            if (health != null) health.Heal(health.Max);
        }
    }
}
