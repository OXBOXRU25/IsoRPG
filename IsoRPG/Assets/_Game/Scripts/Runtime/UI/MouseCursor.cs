using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using IsoRPG.Player;

namespace IsoRPG.UI
{
    /// <summary>
    /// Курсор игры: латная перчатка вместо системной стрелки.
    ///
    /// Указатель — часть облика, а не мелочь: системная стрелка поверх
    /// нарисованного мира читается как «игра ещё не доделана». Рисунок
    /// заказан Павлом 03.09.2026 под наш набор, состояний два — обычное и
    /// со свечением, когда под курсором есть с чем взаимодействовать.
    ///
    /// Горячая точка — кончик указательного пальца, и он намеренно выведен
    /// художником в самый угол картинки: тогда точка нажатия совпадает с
    /// углом, как у обычной стрелки, и при смене состояния прицел не
    /// съезжает ни на пиксель.
    ///
    /// ЦЕНА В КАДРЕ. Это ММО: щупать мир лучом каждый кадр у каждого игрока
    /// нельзя. Щупаем по расписанию, десять раз в секунду — глазом эта
    /// задержка не ловится, потому что курсор и так меняется на границе
    /// объекта, — и зовём смену курсора только когда состояние ДРУГОЕ:
    /// <c>Cursor.SetCursor</c> идёт в драйвер и каждый кадр его дёргать
    /// незачем.
    /// </summary>
    public sealed class MouseCursor : MonoBehaviour
    {
        [Tooltip("Обычный указатель — латная перчатка.")]
        [SerializeField] private Texture2D normal;

        [Tooltip("Указатель над тем, с чем можно взаимодействовать: та же перчатка со свечением.")]
        [SerializeField] private Texture2D glow;

        [Tooltip("Сколько раз в секунду щупаем мир под курсором.")]
        [SerializeField] private float rate = 10f;

        private PlayerInputRouter router;
        private Camera cam;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        private float nextProbe;
        private bool showingGlow;
        private bool applied;

        /// <summary>Задать картинки из сборщика сцены.</summary>
        public void Setup(Texture2D plain, Texture2D lit)
        {
            normal = plain;
            glow = lit;
        }

        private void Start()
        {
            router = GetComponent<PlayerInputRouter>();
            Apply(false, force: true);
        }

        private void OnDisable()
        {
            // Возвращаем системный: иначе в меню и после выхода из сцены
            // остаётся наша перчатка, а это уже не наша территория.
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            applied = false;
        }

        private void Update()
        {
            if (normal == null) return;

            if (Time.unscaledTime < nextProbe) return;
            nextProbe = Time.unscaledTime + 1f / Mathf.Max(1f, rate);

            // Над окном интерфейса перчатка обычная: там взаимодействие своё,
            // и свечение поверх ячейки сумки сбивало бы с толку.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Apply(false);
                return;
            }

            Apply(WorthGlow());
        }

        /// <summary>Есть ли под курсором то, с чем можно что-то сделать.</summary>
        private bool WorthGlow()
        {
            if (router == null) return false;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            var mouse = Mouse.current;
            if (mouse == null) return false;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            int count = Physics.RaycastNonAlloc(ray, hits, router.RayDistance,
                                                router.ClickMask,
                                                QueryTriggerInteraction.Collide);
            if (count == 0) return false;

            if (count > 1)
                System.Array.Sort(hits, 0, count,
                                  System.Collections.Generic.Comparer<RaycastHit>.Create(
                                      (a, b) => a.distance.CompareTo(b.distance)));

            var pick = WorldPick.From(hits, count, gameObject);
            if (!pick.Found) return false;

            // Враг сюда пока не входит: под него заказан отдельный указатель
            // с мечом, и подсвечивать его перчаткой значило бы обещать
            // разговор там, где будет драка.
            return pick.Kind == PickKind.Talk
                || pick.Kind == PickKind.Trade
                || pick.Kind == PickKind.Chest
                || pick.Kind == PickKind.Loot;
        }

        private void Apply(bool lit, bool force = false)
        {
            if (!force && applied && lit == showingGlow) return;

            var texture = lit && glow != null ? glow : normal;
            if (texture == null) return;

            Cursor.SetCursor(texture, Vector2.zero, CursorMode.Auto);

            showingGlow = lit;
            applied = true;
        }
    }
}
