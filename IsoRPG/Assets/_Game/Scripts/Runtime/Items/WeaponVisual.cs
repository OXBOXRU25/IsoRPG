using UnityEngine;

namespace IsoRPG.Items
{
    /// <summary>
    /// Показывает надетое оружие в руках персонажа.
    ///
    /// До сих пор экипировка меняла только числа: надел кинжал — вырос урон,
    /// снял — упал. Проверить это можно было лишь в окне персонажа, а на
    /// экране человек всё время дрался пустыми руками. Из-за этого и анимация
    /// удара двумя клинками читалась как размахивание руками.
    ///
    /// Модель берётся из описания предмета и вкладывается в кость-держатель.
    /// У набора KayKit для этого заведены отдельные кости handslot.l и
    /// handslot.r — они не участвуют в деформации тела и существуют ровно
    /// затем, чтобы к ним что-то цеплять.
    /// </summary>
    public sealed class WeaponVisual : MonoBehaviour
    {
        /// <summary>
        /// Кости-держатели, по порядку поиска.
        ///
        /// У KayKit это `handslot.r`, у Sidekick таких нет вовсе — там
        /// берём саму кисть `hand_r`. До 02.09.2026 искали только первую,
        /// и у нашего героя оружие не появлялось никогда: в журнале честно
        /// висело «нет костей handslot.r и handslot.l».
        /// </summary>
        [Header("Посадка в руке")]
        [Tooltip("Смещение оружия относительно кости, метры. Ноль годится только для костей-держателей KayKit.")]
        [SerializeField] private Vector3 grip = new Vector3(-0.0904f, 0.0060f, 0.0259f);

        [Tooltip("Доворот оружия в кости, градусы.")]
        [SerializeField] private Vector3 gripAngles = new Vector3(6.47f, 93.00f, 178.34f);

        [Tooltip("То же для левой руки. Не копия правой: копия уводит клинок на 23 см вверх, нужно отражение.")]
        [SerializeField] private Vector3 gripLeft = new Vector3(0.0904f, 0.0060f, 0.0259f);

        [SerializeField] private Vector3 gripAnglesLeft = new Vector3(6.47f, 267.00f, 181.66f);

        /// <summary>
        /// Поставить хват числами из задания `grip-fit`.
        ///
        /// Задание считает доворот по матрице из Blender, а не по углам:
        /// покомпонентная перестановка углов между Blender и Unity не равна
        /// повороту, и именно она дала «кинжал повёрнут не в ту сторону».
        /// Ставить надо и в сцене тоже: значение, заданное и в коде, и в
        /// сцене, работает из сцены.
        /// </summary>
        public void SetGrip(Vector3 offset, Vector3 angles, Vector3 leftOffset, Vector3 leftAngles)
        {
            grip = offset;
            gripAngles = angles;
            gripLeft = leftOffset;
            gripAnglesLeft = leftAngles;
        }

        /// <summary>
        /// Перенять посадку оружия у другого показа — обычно у живого героя.
        ///
        /// Числа хвата подобраны примеркой и лежат в СЦЕНЕ, а не в коде:
        /// значение, заданное в обоих местах, работает из сцены. Витрина же
        /// вешает свой компонент через AddComponent и получает умолчания из
        /// кода — то есть заведомо другой хват. Отсюда Павлон 03.09.2026:
        /// «кинжалы не лежат в руках, один ниже руки, второй в кисти».
        ///
        /// Копия обязана спрашивать оригинал, а не помнить числа сама: иначе
        /// подобранный хват придётся править дважды, и второй раз про него
        /// забудут.
        /// </summary>
        public void CopyGrip(WeaponVisual source)
        {
            if (source == null) return;

            grip = source.grip;
            gripAngles = source.gripAngles;
            gripLeft = source.gripLeft;
            gripAnglesLeft = source.gripAnglesLeft;
        }

        private static readonly string[] RightSlotBones = { "handslot.r", "prop_r", "hand_r" };
        private static readonly string[] LeftSlotBones = { "handslot.l", "prop_l", "hand_l" };

        [SerializeField] private Equipment equipment;

        /// <summary>
        /// Слой, на который класть созданное оружие. Нужен витрине в окне
        /// снаряжения: её модель живёт на своём слое, и кинжал, оставшийся
        /// на слое по умолчанию, попал бы в кадр основной камеры — висящим
        /// в воздухе далеко под картой.
        /// </summary>
        private int forcedLayer = -1;

        /// <summary>
        /// Показывать чужую экипировку. Копии героя в окне отдают ту же
        /// самую — поэтому синхронизировать нечего: источник один.
        ///
        /// Подписываемся и показываем прямо здесь, а не надеемся на OnEnable.
        /// Витрина окна персонажа вешает этот компонент через AddComponent, а
        /// Unity зовёт Awake и OnEnable СРАЗУ, внутри самого вызова — то есть
        /// до Setup. К моменту подписки экипировки ещё не было: событие никто
        /// не слушал, первый показ прошёл вхолостую, и копия героя стояла с
        /// пустыми руками навсегда. Снаружи это выглядело как «витрина не
        /// умеет оружие», хотя умеет — ей просто не сказали, чьё.
        /// </summary>
        public void Setup(Equipment source, int layer = -1)
        {
            if (equipment != null) equipment.Changed -= Refresh;

            equipment = source;
            forcedLayer = layer;

            if (!isActiveAndEnabled) return;

            Subscribe();
            Refresh();
        }

        /// <summary>
        /// Подписка на смену экипировки. Идёт из двух мест — OnEnable и
        /// Setup, — поэтому сперва снимаем: иначе один и тот же Refresh
        /// встал бы в очередь дважды и всё делал бы по два раза.
        /// </summary>
        private void Subscribe()
        {
            if (equipment == null) return;

            equipment.Changed -= Refresh;
            equipment.Changed += Refresh;
        }

        private Transform rightSlot;
        private Transform leftSlot;
        private GameObject rightModel;
        private GameObject leftModel;

        /// <summary>
        /// Слой сжатой кисти. Ставит его задание `hand-pose`, а включаем его
        /// мы: пальцы должны обхватывать рукоять, но только когда она есть.
        ///
        /// Ищется один раз при включении. Вес ставится при смене экипировки, а
        /// не в кадре: это ММО, и лишних вычислений на игрока быть не должно.
        /// </summary>
        private Animator animator;
        private int fistLayer = -1;

        /// <summary>Имя слоя. Ставит его задание `hand-pose`, ищем по нему же.</summary>
        private const string FistLayerName = "Кисть";

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<Equipment>();

            animator = GetComponentInChildren<Animator>(true);

            if (animator != null && animator.runtimeAnimatorController != null)
                fistLayer = animator.GetLayerIndex(FistLayerName);

            // Слоя нет — значит пальцы этим кодом не управляются вовсе, ни
            // сжать, ни разжать. Молчать тут нельзя: снаружи это неотличимо
            // от «хват подобран криво», а лечится совсем другим — прогоном
            // задания `hand-pose` на контроллер героя.
            if (animator != null && fistLayer <= 0)
            {
                string controller = animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "нет вовсе";

                Debug.LogWarning($"[IsoRPG] У «{name}» в контроллере «{controller}» нет слоя " +
                                 $"«{FistLayerName}» — пальцы не сожмутся вокруг рукояти и не " +
                                 "разожмутся при снятии оружия. Прогнать задание hand-pose.");
            }

            rightSlot = FindAnyBone(RightSlotBones);
            leftSlot = FindAnyBone(LeftSlotBones);

            // Молчать тут нельзя: без кости оружие просто не появится, и
            // снаружи это неотличимо от «модель не назначена» или «предмет
            // не надет». Три разные причины с одним симптомом — худший вид
            // отладки.
            if (rightSlot == null && leftSlot == null)
            {
                Debug.LogWarning($"[IsoRPG] У «{name}» нет ни одной кости-держателя " +
                                 $"({string.Join(", ", RightSlotBones)}) — оружие показать некуда.");
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.Changed -= Refresh;
        }

        private void Refresh()
        {
            Show(EquipSlot.MainHand, rightSlot, ref rightModel, grip, gripAngles);

            // У левой руки свои числа: кисти зеркальны, и тот же локальный
            // трансформ кладёт в неё клинок иначе. До 02.09.2026 обе руки
            // получали одно и то же — второй кинжал был повёрнут не так, как
            // первый, хотя выглядело это как «оба кривые».
            Show(EquipSlot.OffHand, leftSlot, ref leftModel, gripLeft, gripAnglesLeft);

            // Пальцы сжимаем только когда в руке что-то есть. Иначе герой
            // ходил бы с вечно стиснутыми кулаками — Павлон 02.09.2026:
            // «пальцы должны сжать рукоять, они вообще не согнуты».
            if (animator == null || fistLayer <= 0) return;

            float weight = rightModel != null || leftModel != null ? 1f : 0f;
            float was = animator.GetLayerWeight(fistLayer);

            animator.SetLayerWeight(fistLayer, weight);

            // Замер, а не догадка. 03.09.2026 Павлон увидел: «снял оружие —
            // руки остались в хвате». По коду вес обязан упасть до нуля, и
            // если он падает, а пальцы согнуты — держит их не этот слой, а
            // сама стойка. Строчка в журнале отвечает на это за один проход
            // игры вместо круга правок вслепую. Событие редкое — смена
            // экипировки, — на кадре не сказывается.
            if (!Mathf.Approximately(was, weight))
                Debug.Log($"[IsoRPG] Кисть: вес слоя {was:0.##} → {weight:0.##} " +
                          $"(правая {(rightModel != null ? "занята" : "пуста")}, " +
                          $"левая {(leftModel != null ? "занята" : "пуста")}).");
        }

        private void Show(EquipSlot slot, Transform bone, ref GameObject current,
                          Vector3 offset, Vector3 angles)
        {
            // Старую модель снимаем всегда, даже если новой не будет: иначе
            // снятый кинжал останется висеть в руке.
            if (current != null)
            {
                Destroy(current);
                current = null;
            }

            if (bone == null || equipment == null) return;

            var stack = equipment.GetSlot(slot);
            if (stack.IsEmpty || stack.Item == null || stack.Item.worldModel == null) return;

            current = Instantiate(stack.Item.worldModel, bone);
            current.name = "Weapon_" + slot;

            // Посадка в руке.
            //
            // Нули стояли под кости-держатели KayKit: у них своя ось уже
            // развёрнута под оружие, и доворот не нужен. У Sidekick мы цепляем
            // за саму кисть `hand_r`, а у неё ось идёт вдоль пальцев — клинок
            // с нулями торчит поперёк ладони.
            //
            // Числа сняты примеркой в Blender (соседний чат, 02.09.2026,
            // память проекта `dagger-grip-fit`) и пересчитаны заданием
            // `grip-fit` — по матрице, а не по углам. Прежний перенос
            // переставлял компоненты углов Эйлера, и это и было «кинжал
            // повёрнут не в ту сторону»: у Blender вертикаль Z и порядок
            // XYZ, у нас Y и ZXY, перестановка компонент повороту не равна.
            // Смотреть результат надо на боевой анимации, а не в покое:
            // в покое кисть висит иначе.
            current.transform.localPosition = offset;
            current.transform.localRotation = Quaternion.Euler(angles);
            current.transform.localScale = Vector3.one;

            // Коллайдеры у оружия снимаем: клик по земле рядом с персонажем
            // иначе попадает в лезвие, и он никуда не идёт. Тот же класс
            // ошибки, что был с коллайдером самого игрока.
            foreach (var collider in current.GetComponentsInChildren<Collider>())
                Destroy(collider);

            if (forcedLayer >= 0)
                foreach (var child in current.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = forcedLayer;
        }

        /// <summary>
        /// Ищет кость по имени во всей иерархии, включая выключенные ветки.
        /// </summary>
        private Transform FindBone(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;

            return null;
        }

        /// <summary>Первая найденная кость из списка: наборы называют её по-разному.</summary>
        private Transform FindAnyBone(string[] names)
        {
            foreach (var boneName in names)
            {
                var bone = FindBone(boneName);
                if (bone != null) return bone;
            }

            return null;
        }
    }
}
