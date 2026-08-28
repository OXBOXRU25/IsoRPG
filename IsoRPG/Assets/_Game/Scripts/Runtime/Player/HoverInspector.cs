using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using IsoRPG.Combat;

namespace IsoRPG.Player
{
    /// <summary>
    /// Наведение мышью на мир: показывает, кто под курсором и что с ним
    /// сделает нажатие.
    ///
    /// Отдельно от обработчика клика, хотя цепочку опознания они делят на
    /// двоих через <see cref="WorldPick"/>. Причина в том, что клик — это
    /// событие, а наведение — состояние: оно живёт всё время, пока мышь
    /// стоит на месте, и должно гаснуть само, когда она уходит. Сложить
    /// событие и состояние в один компонент — верный способ получить панель,
    /// которая иногда не исчезает.
    /// </summary>
    public sealed class HoverInspector : MonoBehaviour
    {
        [Tooltip("Сколько раз в секунду щупаем мир под курсором.")]
        [SerializeField] private float rate = 20f;

        private PlayerInputRouter router;
        private CombatHud hud;
        private Camera cam;

        /// <summary>
        /// Буфер под попадания. Поле, а не переменная в методе: щуп работает
        /// двадцать раз в секунду, и RaycastAll на каждом заходе выделял бы
        /// новый массив — сборщику мусора это заметно, а нам не нужно.
        /// </summary>
        private readonly RaycastHit[] hits = new RaycastHit[16];

        private float nextProbe;
        private Vector2 lastPointer;
        private bool showing;
        private WorldPick lastPick = WorldPick.Nothing;

        private void Awake()
        {
            router = GetComponent<PlayerInputRouter>();
            hud = GetComponentInChildren<CombatHud>();
            cam = Camera.main;
        }

        private void OnDisable()
        {
            // Гасим при выключении: иначе панель останется висеть на экране
            // после смерти героя или при переходе между сценами.
            if (hud != null) hud.HideHover();
            showing = false;
        }

        /// <summary>
        /// Показывать ли панель под курсором.
        ///
        /// Выключено по решению Павла: панель с портретом, всплывающая у
        /// указателя, перекрывает то, на что смотришь, и в бою мешает
        /// больше, чем помогает. В WoW под курсором показывают короткую
        /// табличку в углу, а не карточку посреди экрана — если вернёмся к
        /// подсказкам, делать надо так.
        ///
        /// Оставлено переключателем: щуп продолжает работать, просто молча.
        /// </summary>
        public static bool Enabled = false;

        private void Update()
        {
            if (!Enabled)
            {
                if (showing && hud != null) { hud.HideHover(); showing = false; }
                return;
            }

            if (hud == null || router == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pointer = mouse.position.ReadValue();

            // Курсор над интерфейсом — мир не щупаем вовсе. Иначе панель
            // выскакивала бы поверх сумки от того, что ЗА сумкой стоит
            // монстр, и накрывала бы ячейки, по которым человек целится.
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                if (showing) { hud.HideHover(); showing = false; }
                return;
            }

            // Панель едет за курсором каждый кадр, а мир щупается по расписанию:
            // отставание картинки от мыши видно сразу, а лишний луч — нет.
            bool moved = pointer != lastPointer;
            lastPointer = pointer;

            if (Time.unscaledTime < nextProbe)
            {
                if (showing && moved) Repaint(pointer, probe: false);
                return;
            }

            nextProbe = Time.unscaledTime + 1f / Mathf.Max(1f, rate);
            Repaint(pointer, probe: true);
        }

        /// <summary>Обновить панель. probe = искать заново, иначе только сдвинуть.</summary>
        private void Repaint(Vector2 pointer, bool probe)
        {
            if (!probe)
            {
                // Состав панели тот же, меняется только место. Пересобирать
                // её ради сдвига незачем — но и отдельного метода «подвинь»
                // заводить не хочется, поэтому шлём то же самое ещё раз.
                if (lastPick.Found) Send(lastPick, pointer);
                return;
            }

            Ray ray = cam.ScreenPointToRay(pointer);

            int count = Physics.RaycastNonAlloc(ray, hits, router.RayDistance,
                                                router.ClickMask,
                                                QueryTriggerInteraction.Collide);

            if (count > 1)
                System.Array.Sort(hits, 0, count,
                                  System.Collections.Generic.Comparer<RaycastHit>.Create(
                                      (a, b) => a.distance.CompareTo(b.distance)));

            var pick = WorldPick.From(hits, count, gameObject);

            if (!pick.Found)
            {
                if (showing) { hud.HideHover(); showing = false; }
                lastPick = pick;
                return;
            }

            lastPick = pick;
            Send(pick, pointer);
        }

        private void Send(WorldPick pick, Vector2 pointer)
        {
            Sprite face = null;

            // Лицо ищем по имени — тем же справочником, что и панели боя.
            // У сундука и мешка лица нет и не будет: круг портрета просто
            // останется пустым, это лучше, чем случайная физиономия.
            if (pick.Kind == PickKind.Talk) face = Portraits.QuestGiver();
            else if (pick.Kind != PickKind.Chest && pick.Kind != PickKind.Loot)
                face = Portraits.For(pick.RawName);

            int health = 0, maxHealth = 0;

            if (pick.Attackable && pick.Target != null && pick.Target.Health != null)
            {
                health = pick.Target.Health.Current;
                maxHealth = pick.Target.Health.Max;
            }

            hud.ShowHover(pick.Name, pick.Hint, face,
                          pick.Attackable, health, maxHealth, pointer);

            showing = true;
        }
    }
}
